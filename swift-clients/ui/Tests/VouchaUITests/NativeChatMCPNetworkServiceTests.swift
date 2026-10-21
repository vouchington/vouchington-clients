import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPNetworkServiceTests: XCTestCase {
    func testAppleOAuthOnlyEnablesTheRegisteredProductionOrigin() throws {
        for origin in ["https://voucha.ai", "https://voucha.ai/"] {
            XCTAssertTrue(try NativeChatMCPAppleOAuthOrigin.allows(XCTUnwrap(URL(string: origin))))
        }
        for origin in [
            "https://staging.voucha.ai", "https://voucha.ai.example.test", "http://voucha.ai",
            "https://voucha.ai:8443", "https://voucha.ai/api", "https://voucha.ai/?preview=1"
        ] {
            XCTAssertFalse(try NativeChatMCPAppleOAuthOrigin.allows(XCTUnwrap(URL(string: origin))))
        }
    }

    func testDiscoveryFailureDoesNotPinDifferentAccount() async throws {
        MCPAgentURLProtocol.configure(discoveryUnavailable: true)
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: HeldClearMCPTokenStore(holdClear: false),
            browserCode: { _, _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        do {
            _ = try await service.connect(accountId: "member-a")
            XCTFail("Discovery should fail")
        } catch NativeChatMCPNetworkService.Failure.accountMismatch {
            XCTFail("Discovery failure must not pin the account")
        } catch {}
        MCPAgentURLProtocol.configure()
        _ = try await service.connect(accountId: "member-b")
    }

    func testConnectAndRefreshCollectAllCatalogPages() async throws {
        for emptyCursor in [false, true] {
            MCPAgentURLProtocol.configure(paginatedTools: true, emptyContinuationCursor: emptyCursor)
            let site = try XCTUnwrap(URL(string: "https://example.test"))
            let service = try NativeChatMCPNetworkService(
                siteOrigin: site, app: .macos, tokenStore: HeldClearMCPTokenStore(holdClear: false),
                browserCode: { _, _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
            )
            for catalog in try await [
                service.connect(accountId: "member-a"),
                service.refreshTools(accountId: "member-a")
            ] {
                guard case let .object(result) = catalog,
                      case let .array(tools)? = result["tools"] else {
                    return XCTFail("Expected merged tool catalog")
                }
                XCTAssertEqual(tools.count, 2)
                XCTAssertNil(result["nextCursor"])
            }
        }
    }

    func testOAuthReceivesTheWindowPassedByTheInitiatingChat() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let initiatingWindow = NSObject()
        var receivedWindow: AnyObject?
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: HeldClearMCPTokenStore(holdClear: false),
            browserCode: { _, window in
                receivedWindow = window
                return "new-code"
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )

        _ = try await service.connect(accountId: "member-a", presentationWindow: initiatingWindow)
        XCTAssertTrue(receivedWindow === initiatingWindow)
    }

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
            browserCode: { _, _ in browserCalls += 1
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
            browserCode: { _, _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
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
            browserCode: { _, _ in "new-code" }, protocolClasses: [MCPAgentURLProtocol.self]
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
            browserCode: { _, _ in XCTFail("Sign-out must not present a browser")
                return ""
            },
            protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let saved = try await store.load(scope: scope)
        XCTAssertNil(saved)
        await expectRevokeCount(1)
    }

    func testStalledRemoteCloseDoesNotDelayLocalGrantRemoval() async throws {
        MCPAgentURLProtocol.configure(holdClose: true)
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let scope = scope(for: "member-a", site: site)
        try await store.save(
            .init(
                accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
                scope: "mcp.user:read", tokenType: "Bearer"
            ), scope: scope
        )
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _, _ in XCTFail("Existing grant must avoid browser authorization")
                return ""
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")

        let disconnect = Task {
            try await service.disconnect(accountId: "member-a")
            await MCPAgentURLProtocol.waitForHeldClose()
            return MCPAgentURLProtocol.isCloseHeld
        }
        await MCPAgentURLProtocol.waitForHeldClose()
        let release = Task {
            try? await Task.sleep(for: .seconds(2))
            MCPAgentURLProtocol.releaseHeldClose()
        }
        let returnedWhileCloseWasHeld = try await disconnect.value
        XCTAssertTrue(returnedWhileCloseWasHeld)
        let retainedGrant = try await store.load(scope: scope)
        XCTAssertNil(retainedGrant)
        await release.value
    }

    func testDisconnectDoesNotRefreshNearlyExpiredAccessTokenBeforeClearingGrant() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let accountScope = scope(for: "member-a", site: site)
        try await store.save(.init(
            accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer"
        ), scope: accountScope)
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _, _ in XCTFail("Existing grant must avoid browser authorization")
                return ""
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")
        try await store.save(.init(
            accessToken: "near-expiry", refreshToken: "old-refresh", expiresIn: 1,
            scope: "mcp.user:read", tokenType: "Bearer"
        ), scope: accountScope)

        try await service.disconnect(accountId: "member-a")

        let cleared = try await store.load(scope: accountScope)
        XCTAssertNil(cleared)
        XCTAssertEqual(MCPAgentURLProtocol.tokenRequestCount, 0)
        await expectRevokeCount(1)
    }

    func testLiveSignOutClearsOldIssuerScopesBeforeStalledRemoteRevoke() async throws {
        MCPAgentURLProtocol.configure(holdRevoke: true)
        defer { MCPAgentURLProtocol.releaseHeldRevoke() }
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let currentScope = scope(for: "member-a", site: site)
        let olderScope = MemberMCPOAuthTokenScope(
            accountId: "member-a", issuerIdentifier: "https://old-issuer.test",
            resource: currentScope.resource, clientId: currentScope.clientId
        )
        let otherScope = scope(for: "member-b", site: site)
        let credentials = MemberMCPOAuthTokens(
            accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer"
        )
        for storedScope in [currentScope, olderScope, otherScope] {
            try await store.save(credentials, scope: storedScope)
        }
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _, _ in XCTFail("Existing grant must avoid browser authorization")
                return ""
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        _ = try await service.connect(accountId: "member-a")

        try await service.disconnect(accountId: "member-a")

        let currentGrant = try await store.load(scope: currentScope)
        let olderGrant = try await store.load(scope: olderScope)
        let otherGrant = try await store.load(scope: otherScope)
        XCTAssertNil(currentGrant)
        XCTAssertNil(olderGrant)
        XCTAssertNotNil(otherGrant)
        for _ in 0 ..< 100 where !MCPAgentURLProtocol.isRevokeHeld {
            try? await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertTrue(MCPAgentURLProtocol.isRevokeHeld)
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
            browserCode: { _, _ in XCTFail("Sign-out must not present a browser")
                return ""
            },
            protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let cleared = try await store.load(scope: scope)
        let retained = try await store.load(scope: other)
        XCTAssertNil(cleared)
        XCTAssertNotNil(retained)
    }

    func testColdSignOutClearsOldIssuerGrantAfterCurrentMetadataChanges() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let oldAccountScope = MemberMCPOAuthTokenScope(
            accountId: "member-a", issuerIdentifier: "https://old-issuer.test",
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/old-client")
        )
        let otherAccountScope = MemberMCPOAuthTokenScope(
            accountId: "member-b", issuerIdentifier: oldAccountScope.issuerIdentifier,
            resource: oldAccountScope.resource, clientId: oldAccountScope.clientId
        )
        let credentials = MemberMCPOAuthTokens(
            accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer"
        )
        try await store.save(credentials, scope: oldAccountScope)
        try await store.save(credentials, scope: otherAccountScope)
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _, _ in XCTFail("Sign-out must not present a browser")
                return ""
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let cleared = try await store.load(scope: oldAccountScope)
        let retained = try await store.load(scope: otherAccountScope)
        XCTAssertNil(cleared)
        XCTAssertNotNil(retained)
    }

    func testColdSignOutClearsGrantBeforeStalledOAuthDiscovery() async throws {
        MCPAgentURLProtocol.configure(holdDiscovery: true)
        defer { MCPAgentURLProtocol.releaseHeldDiscovery() }
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let store = HeldClearMCPTokenStore(holdClear: false)
        let currentScope = scope(for: "member-a", site: site)
        try await store.save(.init(
            accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer"
        ), scope: currentScope)
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: store,
            browserCode: { _, _ in XCTFail("Sign-out must not present a browser")
                return ""
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )

        try await service.disconnect(accountId: "member-a")

        let cleared = try await store.load(scope: currentScope)
        XCTAssertNil(cleared)
        for _ in 0 ..< 100 where !MCPAgentURLProtocol.isDiscoveryHeld {
            try? await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertTrue(MCPAgentURLProtocol.isDiscoveryHeld)
    }

    private func scope(for account: String, site: URL) -> MemberMCPOAuthTokenScope {
        MemberMCPOAuthTokenScope(
            accountId: account, issuerIdentifier: site.absoluteString,
            resource: site.appendingPathComponent("api/v1/mcp"),
            clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos")
        )
    }

    private func expectRevokeCount(_ expected: Int) async {
        for _ in 0 ..< 100 where MCPAgentURLProtocol.revokeCount < expected {
            try? await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertEqual(MCPAgentURLProtocol.revokeCount, expected)
    }
}

actor HeldClearMCPTokenStore: MemberMCPOAuthTokenStore, MemberMCPOAuthAccountClearing {
    private let holdClear: Bool
    private var values: [MemberMCPOAuthTokenScope: MemberMCPOAuthTokens] = [:]
    private var started: [String: CheckedContinuation<Void, Never>] = [:]
    private var releases: [String: CheckedContinuation<Void, Never>] = [:]
    private var clearingAccounts: Set<String> = []
    private var heldOnce: Set<String> = []
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

    func takeAccount(_ accountId: String) async -> [MemberMCPOAuthStoredGrant] {
        if holdClear, heldOnce.insert(accountId).inserted {
            clearingAccounts.insert(accountId)
            started.removeValue(forKey: accountId)?.resume()
            await withCheckedContinuation { releases[accountId] = $0 }
            clearingAccounts.remove(accountId)
        }
        let grants = values.compactMap { scope, tokens -> MemberMCPOAuthStoredGrant? in
            scope.accountId == accountId ? .init(scope: scope, tokens: tokens) : nil
        }
        values = values.filter { $0.key.accountId != accountId }
        return grants
    }

    func waitForClear(accountId: String) async {
        if clearingAccounts.contains(accountId) { return }
        await withCheckedContinuation { started[accountId] = $0 }
    }

    func releaseClear(accountId: String) {
        releases.removeValue(forKey: accountId)?.resume()
    }
}
