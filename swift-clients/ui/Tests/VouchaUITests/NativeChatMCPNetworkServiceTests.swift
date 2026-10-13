import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPNetworkServiceTests: XCTestCase {
    func testReconnectWaitsForPreviousGrantCleanupAndPreservesNewGrant() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore()
        let scope = MemberMCPOAuthTokenScope(
            accountId: "member-a", issuerIdentifier: site.absoluteString,
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos")
        )
        try await store.save(
            .init(
                accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            ), scope: scope
        )
        var browserCalls = 0
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _ in browserCalls += 1
                return "new-code"
            },
            protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")
        let disconnect = Task { try await service.disconnect(accountId: "member-a") }
        await store.waitForClear(accountId: "member-a")

        let reconnect = Task { try await service.connect(accountId: "member-a") }
        await Task.yield()
        XCTAssertEqual(browserCalls, 0)
        let prematureLoads = await store.prematureLoadCount
        XCTAssertEqual(prematureLoads, 0)

        await store.releaseClear(accountId: "member-a")
        try await disconnect.value
        _ = try await reconnect.value
        XCTAssertEqual(browserCalls, 1)
        let saved = try await store.load(scope: scope)
        XCTAssertEqual(saved?.accessToken, "new-access")
        XCTAssertEqual(saved?.refreshToken, "new-refresh")
    }

    func testReconnectWaitsForSecondQueuedSignOutAfterFirstCompletes() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore()
        for account in ["member-a", "member-b"] {
            try await store.save(
                .init(
                    accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
                    scope: "mcp.user:read", tokenType: "Bearer"
                ), scope: scope(for: account, site: site)
            )
        }
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")
        let first = Task { try await service.disconnect(accountId: "member-a") }
        await store.waitForClear(accountId: "member-a")
        let second = Task { try await service.disconnect(accountId: "member-b") }
        await store.releaseClear(accountId: "member-a")
        try await first.value
        await store.waitForClear(accountId: "member-b")

        let reconnect = Task { try await service.connect(accountId: "member-a") }
        await Task.yield()
        let prematureLoads = await store.prematureLoadCount
        XCTAssertEqual(prematureLoads, 0)

        await store.releaseClear(accountId: "member-b")
        try await second.value
        _ = try await reconnect.value
    }

    func testCancelledConnectCannotClaimAccountAfterPendingCleanup() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore()
        try await store.save(
            .init(
                accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            ), scope: scope(for: "member-a", site: site)
        )
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")
        let disconnect = Task { try await service.disconnect(accountId: "member-a") }
        await store.waitForClear(accountId: "member-a")
        let cancelled = Task { try await service.connect(accountId: "member-b") }
        cancelled.cancel()
        await store.releaseClear(accountId: "member-a")
        try await disconnect.value
        do {
            _ = try await cancelled.value
            XCTFail("Cancelled account claim must not resume after cleanup")
        } catch is CancellationError {}
        _ = try await service.connect(accountId: "member-c")
    }

    func testFreshServiceSignOutRevokesPersistedGrantBeforeConnect() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let scope = MemberMCPOAuthTokenScope(
            accountId: "member-a", issuerIdentifier: site.absoluteString,
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos")
        )
        try await store.save(
            .init(
                accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            ), scope: scope
        )
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _ in XCTFail("Sign-out must not present a browser")
                return ""
            },
            protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let saved = try await store.load(scope: scope)
        XCTAssertNil(saved)
        XCTAssertEqual(MCPAgentURLProtocol.revokeCount, 1)
    }

    func testOfflineColdSignOutClearsOnlyFormerAccountGrant() async throws {
        MCPAgentURLProtocol.configure(discoveryUnavailable: true)
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let scope = MemberMCPOAuthTokenScope(
            accountId: "member-a", issuerIdentifier: site.absoluteString,
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos")
        )
        let other = MemberMCPOAuthTokenScope(
            accountId: "member-b", issuerIdentifier: site.absoluteString,
            resource: scope.resource, clientId: scope.clientId
        )
        let credentials = MemberMCPOAuthTokens(
            accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer"
        )
        try await store.save(credentials, scope: scope)
        try await store.save(credentials, scope: other)
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _ in XCTFail("Sign-out must not present a browser")
                return ""
            },
            protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let cleared = try await store.load(scope: scope)
        let retained = try await store.load(scope: other)
        XCTAssertNil(cleared)
        XCTAssertNotNil(retained)
        XCTAssertEqual(MCPAgentURLProtocol.revokeCount, 0)
    }

    private func scope(for account: String, site: URL) -> MemberMCPOAuthTokenScope {
        MemberMCPOAuthTokenScope(
            accountId: account, issuerIdentifier: site.absoluteString,
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos")
        )
    }
}

private actor HeldClearMCPTokenStore: MemberMCPOAuthTokenStore, MemberMCPOAuthAccountClearing {
    private let holdClear: Bool
    private var values: [MemberMCPOAuthTokenScope: MemberMCPOAuthTokens] = [:]
    private var started: [String: CheckedContinuation<Void, Never>] = [:]
    private var releases: [String: CheckedContinuation<Void, Never>] = [:]
    private var clearingAccounts: Set<String> = []
    private(set) var prematureLoadCount = 0

    init(holdClear: Bool = true) {
        self.holdClear = holdClear
    }

    func load(scope: MemberMCPOAuthTokenScope) throws -> MemberMCPOAuthTokens? {
        if !clearingAccounts.isEmpty { prematureLoadCount += 1 }
        return values[scope]
    }

    func save(_ tokens: MemberMCPOAuthTokens, scope: MemberMCPOAuthTokenScope) throws {
        values[scope] = tokens
    }

    func clear(scope: MemberMCPOAuthTokenScope) async throws {
        if !holdClear {
            values[scope] = nil
            return
        }
        clearingAccounts.insert(scope.accountId)
        started.removeValue(forKey: scope.accountId)?.resume()
        await withCheckedContinuation { releases[scope.accountId] = $0 }
        values[scope] = nil
        clearingAccounts.remove(scope.accountId)
    }

    func clearAccount(_ accountId: String) throws {
        values = values.filter { $0.key.accountId != accountId }
    }

    func waitForClear(accountId: String) async {
        if clearingAccounts.contains(accountId) { return }
        await withCheckedContinuation { started[accountId] = $0 }
    }

    func releaseClear(accountId: String) {
        releases.removeValue(forKey: accountId)?.resume()
    }
}

private final class MCPAgentURLProtocol: URLProtocol {
    private static let stateLock = NSLock()
    private static var unavailable = false
    private static var revoked = 0
    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    static func configure(discoveryUnavailable: Bool = false) {
        stateLock.lock()
        unavailable = discoveryUnavailable
        revoked = 0
        stateLock.unlock()
    }

    static var revokeCount: Int {
        stateLock.lock()
        defer { stateLock.unlock() }
        return revoked
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped, let url = request.url else { return }
        let path = url.path
        let body = request.httpBody ?? request.httpBodyStream.map(Self.readBodyStream)
        let rpc = body.flatMap { (try? JSONSerialization.jsonObject(with: $0)) as? [String: Any] }
        let method = rpc?["method"] as? String
        let id = rpc?["id"] as? String ?? "1"
        Self.stateLock.lock()
        let discoveryUnavailable = Self.unavailable
        if path == "/revoke" { Self.revoked += 1 }
        Self.stateLock.unlock()
        let payload: String
        let status: Int
        switch path {
        case "/.well-known/oauth-protected-resource/api/v1/mcp" where discoveryUnavailable:
            payload = #"{"error":"unavailable"}"#
            status = 503
        case "/.well-known/oauth-protected-resource/api/v1/mcp":
            payload = #"{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://example.test"]}"#
            status = 200
        case "/.well-known/oauth-authorization-server":
            payload = #"{"issuer":"https://example.test","authorization_endpoint":"https://example.test/authorize","token_endpoint":"https://example.test/token","revocation_endpoint":"https://example.test/revoke"}"#
            status = 200
        case "/token":
            payload = #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
            status = 200
        case "/revoke":
            payload = ""
            status = 200
        case "/api/v1/mcp" where request.httpMethod == "DELETE":
            payload = ""
            status = 204
        case "/api/v1/mcp" where method == "initialize":
            payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"protocolVersion":"2025-06-18"}}"#
            status = 200
        case "/api/v1/mcp" where method == "notifications/initialized":
            payload = ""
            status = 202
        case "/api/v1/mcp" where method == "tools/list":
            payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"tools":[{"name":"read_post"}]}}"#
            status = 200
        default:
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        let response = HTTPURLResponse(
            url: url, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        if !payload.isEmpty { client?.urlProtocol(self, didLoad: Data(payload.utf8)) }
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        stopped = true
        deliveryLock.unlock()
    }

    private static func readBodyStream(_ stream: InputStream) -> Data {
        stream.open()
        defer { stream.close() }
        var data = Data()
        var bytes = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&bytes, maxLength: bytes.count)
            if count <= 0 { break }
            data.append(bytes, count: count)
        }
        return data
    }
}
