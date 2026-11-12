import Foundation
import VouchaAPI
import VouchaModels

/// Connects member chat to the MCP catalog with a token minted from the signed-in native session.
/// There is no browser authorization, consent screen, or scope request: the server decides read or
/// write access by plan and the catalog is exactly what it returns. Tokens live in memory only.
@MainActor
public final class NativeChatMCPSessionBoundService: NativeChatMCPServicing {
    public enum Failure: Error { case accountMismatch }

    private struct Context {
        let accountId: String
        let tokens: MemberMCPSessionTokenSource
        let authorized: MemberMCPSessionAuthorizedClient
    }

    private let siteOrigin: URL
    private let app: MemberMCPApp
    private let apiClient: APIClient
    private let currentAccountId: @MainActor () -> String?
    private let legacyGrantCleanup: (@MainActor (String) async throws -> Void)?
    private let protocolClasses: [AnyClass]?
    private var context: Context?
    private var generation = 0
    private let remoteCloseTimeout: TimeInterval
    /// The bounded best-effort remote close started by the last `disconnect`; exposed for tests.
    private(set) var remoteClose: Task<Void, Never>?

    /// - Parameter legacyGrantCleanup: removes grants stored by the retired browser authorization.
    public init(
        siteOrigin: URL,
        app: MemberMCPApp,
        apiClient: APIClient,
        currentAccountId: @escaping @MainActor () -> String?,
        legacyGrantCleanup: (@MainActor (String) async throws -> Void)? = nil,
        protocolClasses: [AnyClass]? = nil,
        remoteCloseTimeout: TimeInterval = 5
    ) {
        self.remoteCloseTimeout = remoteCloseTimeout
        self.siteOrigin = siteOrigin
        self.app = app
        self.apiClient = apiClient
        self.currentAccountId = currentAccountId
        self.legacyGrantCleanup = legacyGrantCleanup
        self.protocolClasses = protocolClasses
    }

    public func connect(accountId: String, presentationWindow _: AnyObject?) async throws -> DecodedJSONValue {
        try Task.checkCancellation()
        generation += 1
        let expected = generation
        let current = try makeContext(accountId)
        return try await listAllTools(current, generation: expected)
    }

    /// The requested scope is ignored: server plan policy owns read/write authority.
    public func authorize(
        accountId: String, scope _: String, presentationWindow: AnyObject?
    ) async throws -> DecodedJSONValue {
        try await connect(accountId: accountId, presentationWindow: presentationWindow)
    }

    public func refreshTools(accountId: String) async throws -> DecodedJSONValue {
        guard let context, context.accountId == accountId else { throw Failure.accountMismatch }
        return try await listAllTools(context, generation: generation)
    }

    public func callTool(
        accountId: String, name: String, arguments: DecodedJSONObject
    ) async throws -> MemberMCPClient.ToolResult {
        guard let context, context.accountId == accountId else { throw Failure.accountMismatch }
        let expected = generation
        let result = try await context.authorized.callTool(name: name, arguments: arguments)
        try assertCurrent(accountId, generation: expected)
        return result
    }

    public func disconnect(accountId: String) async throws {
        generation += 1
        if let live = context, live.accountId == accountId {
            context = nil
            // The token is dropped here; the remote DELETE runs bounded in the background so a stalled
            // server cannot hold up sign-out.
            remoteClose = await live.authorized.disconnect(closeTimeout: remoteCloseTimeout)
        }
        try await legacyGrantCleanup?(accountId)
    }

    private func makeContext(_ accountId: String) throws -> Context {
        if let context {
            guard context.accountId == accountId else { throw Failure.accountMismatch }
            return context
        }
        let currentAccountId = currentAccountId
        let tokens = MemberMCPSessionTokenSource(
            client: apiClient, app: app, resource: siteOrigin.appendingPathComponent("api/v1/mcp"),
            isCurrent: { await MainActor.run { currentAccountId() == accountId } }
        )
        let client = try MemberMCPClient(siteOrigin: siteOrigin, protocolClasses: protocolClasses)
        let made = Context(
            accountId: accountId, tokens: tokens,
            authorized: MemberMCPSessionAuthorizedClient(client: client, tokens: tokens)
        )
        context = made
        return made
    }

    private func assertCurrent(_ accountId: String, generation: Int) throws {
        try Task.checkCancellation()
        guard self.generation == generation, context?.accountId == accountId else { throw CancellationError() }
    }

    private func listAllTools(_ current: Context, generation: Int) async throws -> DecodedJSONValue {
        let tools = try await NativeChatMCPToolCatalog.listAll(
            page: { cursor in try await current.authorized.listTools(cursor: cursor) },
            assertCurrent: { try self.assertCurrent(current.accountId, generation: generation) }
        )
        try assertCurrent(current.accountId, generation: generation)
        return tools
    }
}

public extension NativeChatMCPSessionBoundService {
    /// Clears grants a retired browser authorization left in the Keychain, then revokes them remotely.
    static func legacyGrantCleanup(
        siteOrigin: URL, app: MemberMCPApp, tokenStore: any MemberMCPOAuthTokenStore
    ) -> @MainActor (String) async throws -> Void {
        { accountId in
            try await NativeChatMCPPersistedGrantCleanup.signOut(.init(
                accountId: accountId, siteOrigin: siteOrigin, app: app,
                discovery: MemberMCPOAuthDiscovery(siteOrigin: siteOrigin),
                tokenStore: tokenStore, protocolClasses: nil
            ))
        }
    }
}
