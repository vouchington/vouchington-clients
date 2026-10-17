import Foundation
import VouchaAPI
import VouchaModels

/// The browser callback is supplied by the Apple app, keeping presentation in the app layer.
@MainActor
public final class NativeChatMCPNetworkService: NativeChatMCPServicing {
    public enum Failure: Error { case accountMismatch, invalidToolCatalog, accountClearingUnavailable }

    public typealias BrowserCode = @MainActor (MemberMCPOAuthRequest, AnyObject?) async throws -> String

    private struct Context {
        let accountId: String
        let metadata: MemberMCPOAuthMetadata
        let client: MemberMCPClient
        let tokens: MemberMCPTokenManager
        let authorized: MemberMCPAuthorizedClient
    }

    private let siteOrigin: URL
    private let app: MemberMCPApp
    private let discovery: MemberMCPOAuthDiscovery
    private let tokenStore: any MemberMCPOAuthTokenStore
    private let browserCode: BrowserCode
    private let protocolClasses: [AnyClass]?
    private var context: Context?
    private var activeAccountId: String?
    private var generation = 0
    private var pendingDisconnect: Task<Void, Error>?
    private var pendingDisconnectId = 0

    public init(
        siteOrigin: URL,
        app: MemberMCPApp,
        tokenStore: any MemberMCPOAuthTokenStore,
        browserCode: @escaping BrowserCode,
        protocolClasses: [AnyClass]? = nil
    ) throws {
        self.siteOrigin = siteOrigin
        self.app = app
        self.tokenStore = tokenStore
        self.browserCode = browserCode
        self.protocolClasses = protocolClasses
        discovery = try MemberMCPOAuthDiscovery(siteOrigin: siteOrigin, protocolClasses: protocolClasses)
    }

    public func connect(accountId: String) async throws -> DecodedJSONValue {
        try await connect(accountId: accountId, presentationWindow: nil)
    }

    public func connect(accountId: String, presentationWindow: AnyObject?) async throws -> DecodedJSONValue {
        if let pendingDisconnect { try await pendingDisconnect.value }
        try Task.checkCancellation()
        guard activeAccountId == nil || activeAccountId == accountId else { throw Failure.accountMismatch }
        activeAccountId = accountId
        generation += 1
        let expectedGeneration = generation
        let current: Context
        do {
            current = try await contextForAccount(accountId, generation: expectedGeneration)
        } catch {
            if generation == expectedGeneration, activeAccountId == accountId, context == nil {
                activeAccountId = nil
            }
            throw error
        }
        do {
            let tools = try await listAllTools(current.authorized, accountId: accountId, generation: expectedGeneration)
            try assertCurrent(accountId, generation: expectedGeneration)
            return tools
        } catch MemberMCPTokenManager.Failure.notAuthorized {
            let request = try MemberMCPOAuthRequest(
                siteOrigin: siteOrigin,
                app: app,
                issuerIdentifier: current.metadata.issuerIdentifier,
                authorizationEndpoint: current.metadata.authorizationEndpoint,
                redirectURI: siteOrigin.appendingPathComponent("oauth/native/\(app.rawValue)/callback")
            )
            let code = try await browserCode(request, presentationWindow)
            try assertCurrent(accountId, generation: expectedGeneration)
            _ = try await current.tokens.redeem(
                code: code, verifier: request.verifier, redirectURI: request.redirectURI
            )
            try assertCurrent(accountId, generation: expectedGeneration)
            let tools = try await listAllTools(current.authorized, accountId: accountId, generation: expectedGeneration)
            try assertCurrent(accountId, generation: expectedGeneration)
            return tools
        }
    }

    public func refreshTools(accountId: String) async throws -> DecodedJSONValue {
        guard let context, context.accountId == accountId else { throw Failure.accountMismatch }
        let expectedGeneration = generation
        let tools = try await listAllTools(context.authorized, accountId: accountId, generation: expectedGeneration)
        try assertCurrent(accountId, generation: expectedGeneration)
        return tools
    }

    public func disconnect(accountId: String) async throws {
        guard activeAccountId == nil || activeAccountId == accountId else { return }
        let previousDisconnect = pendingDisconnect
        generation += 1
        activeAccountId = nil
        let current = context?.accountId == accountId ? context : nil
        context = nil
        let task = Task {
            if let previousDisconnect { _ = try? await previousDisconnect.value }
            if let current {
                try await clearLiveGrant(current, accountId: accountId)
            } else {
                try await NativeChatMCPPersistedGrantCleanup.signOut(.init(
                    accountId: accountId, siteOrigin: siteOrigin, app: app, discovery: discovery,
                    tokenStore: tokenStore, protocolClasses: protocolClasses
                ))
            }
        }
        pendingDisconnectId += 1
        let disconnectId = pendingDisconnectId
        pendingDisconnect = task
        defer {
            if pendingDisconnectId == disconnectId { pendingDisconnect = nil }
        }
        try await task.value
    }

    private func contextForAccount(_ accountId: String, generation: Int) async throws -> Context {
        if let context {
            guard context.accountId == accountId else { throw Failure.accountMismatch }
            return context
        }
        let metadata = try await discovery.discover()
        try Task.checkCancellation()
        guard self.generation == generation, activeAccountId == accountId else { throw CancellationError() }
        let clientId = siteOrigin.appendingPathComponent("api/v1/oauth/native-clients/\(app.rawValue)")
        let resource = siteOrigin.appendingPathComponent("api/v1/mcp")
        let tokenClient = try MemberMCPOAuthTokenClient(
            metadata: metadata, clientId: clientId, resource: resource, protocolClasses: protocolClasses
        )
        let tokens = MemberMCPTokenManager(accountId: accountId, store: tokenStore, tokenClient: tokenClient)
        let client = try MemberMCPClient(siteOrigin: siteOrigin, protocolClasses: protocolClasses)
        let context = Context(
            accountId: accountId, metadata: metadata, client: client, tokens: tokens,
            authorized: MemberMCPAuthorizedClient(client: client, tokens: tokens)
        )
        self.context = context
        return context
    }

    private func assertCurrent(_ accountId: String, generation: Int) throws {
        try Task.checkCancellation()
        guard self.generation == generation, context?.accountId == accountId else {
            throw CancellationError()
        }
    }

    private func listAllTools(
        _ authorized: MemberMCPAuthorizedClient, accountId: String, generation: Int
    ) async throws -> DecodedJSONValue {
        try await NativeChatMCPToolCatalog.listAll(authorized: authorized) {
            try self.assertCurrent(accountId, generation: generation)
        }
    }
}

private extension NativeChatMCPNetworkService {
    private func clearLiveGrant(_ current: Context, accountId: String) async throws {
        let drain = await current.tokens.beginLocalSignOut()
        guard let accountClearing = tokenStore as? any MemberMCPOAuthAccountClearing else {
            throw Failure.accountClearingUnavailable
        }
        var grants = try await accountClearing.takeAccount(accountId)
        await drain.value
        grants += try await accountClearing.takeAccount(accountId)
        NativeChatMCPRemoteGrantCleanup.start(.init(
            grants: grants, accountId: accountId, siteOrigin: siteOrigin,
            app: app, discovery: discovery, protocolClasses: protocolClasses,
            knownMetadata: current.metadata, activeClient: current.client
        ))
    }
}

public extension NativeChatMCPNetworkService {
    func callTool(
        accountId: String, name: String, arguments: DecodedJSONObject
    ) async throws -> MemberMCPClient.ToolResult {
        guard let context, context.accountId == accountId,
              activeAccountId == accountId else { throw Failure.accountMismatch }
        let expectedGeneration = generation
        let result = try await context.authorized.callTool(name: name, arguments: arguments)
        try assertCurrent(accountId, generation: expectedGeneration)
        return result
    }
}
