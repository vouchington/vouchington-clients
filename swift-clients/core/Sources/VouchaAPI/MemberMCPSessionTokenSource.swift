import Foundation
import VouchaCore
import VouchaModels

/// Holds the session-bound MCP access token for one signed-in account in memory only. The token is
/// minted through the cookie-bearing API client (App Attest signs it when the client has a signer),
/// never persisted, and never written to refresh-token storage. Mint is single-flight; `clear()`
/// fences any mint that is still in flight.
public actor MemberMCPSessionTokenSource {
    public typealias Failure = MemberMCPTokenManager.Failure

    private struct Minted {
        let accessToken: String
        let expiresAt: Date
    }

    private static let expirySkew: TimeInterval = 30

    private let client: APIClient
    private let app: MemberMCPApp
    private let resource: URL
    private let isCurrent: @Sendable () async -> Bool
    private let now: @Sendable () -> Date
    private var current: Minted?
    private var minting: Task<Minted, Error>?
    private var epoch = 0
    private var cleared = false

    public init(
        client: APIClient,
        app: MemberMCPApp,
        resource: URL,
        isCurrent: @escaping @Sendable () async -> Bool = { true },
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.client = client
        self.app = app
        self.resource = resource
        self.isCurrent = isCurrent
        self.now = now
    }

    public func accessToken() async throws -> String {
        try await admit()
        if let current, current.expiresAt > now() { return current.accessToken }
        current = nil
        let task = startMint()
        let minted: Minted
        do { minted = try await task.value } catch is CancellationError { throw Failure.notAuthorized }
        try await admit()
        return minted.accessToken
    }

    /// A late 401 for an older token reuses a newer mint instead of minting again.
    public func refresh(rejectedAccessToken: String) async throws -> String {
        if current?.accessToken == rejectedAccessToken { current = nil }
        return try await accessToken()
    }

    /// The live token, if any, without minting; used for best-effort remote close.
    public var currentAccessToken: String? {
        cleared ? nil : current?.accessToken
    }

    /// Drops the token at sign-out or account change and fences any mint in flight.
    public func clear() {
        cleared = true
        current = nil
        minting?.cancel()
        minting = nil
        epoch += 1
    }

    private func admit() async throws {
        guard !cleared else { throw Failure.notAuthorized }
        guard await isCurrent() else {
            clear()
            throw Failure.notAuthorized
        }
        guard !cleared else { throw Failure.notAuthorized }
    }

    private func startMint() -> Task<Minted, Error> {
        if let minting { return minting }
        let mintEpoch = epoch
        let task = Task { try await self.mint(epoch: mintEpoch) }
        minting = task
        return task
    }

    private func mint(epoch mintEpoch: Int) async throws -> Minted {
        defer { if epoch == mintEpoch { minting = nil } }
        let response: NativeSessionMCPTokenResponse
        do {
            response = try await client.send(.mintNativeSessionMCPToken(app: app))
        } catch VouchaError.unauthorized {
            throw Failure.notAuthorized
        } catch VouchaError.sessionExpired {
            throw Failure.notAuthorized
        } catch {
            // A cleared source cancels its mint; that surfaces as lost authorization, not a network fault.
            if cleared || epoch != mintEpoch { throw Failure.notAuthorized }
            throw error
        }
        guard response.tokenType.lowercased() == "bearer", !response.accessToken.isEmpty,
              response.expiresIn > 0, URL(string: response.resource) == resource
        else { throw VouchaError.unexpected("The minted MCP token does not match the MCP resource.") }
        guard !cleared, epoch == mintEpoch else { throw Failure.notAuthorized }
        let minted = Minted(
            accessToken: response.accessToken,
            expiresAt: now().addingTimeInterval(TimeInterval(response.expiresIn) - Self.expirySkew)
        )
        current = minted
        return minted
    }
}
