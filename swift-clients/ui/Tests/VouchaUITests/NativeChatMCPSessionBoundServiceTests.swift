import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPSessionBoundServiceTests: XCTestCase {
    private let site = URL(string: "https://example.test")!

    private func makeService(
        account: @escaping @MainActor () -> String? = { "member-a" },
        cleanedAccounts: CleanedAccounts = CleanedAccounts()
    ) -> NativeChatMCPSessionBoundService {
        let client = APIClient(
            config: AppConfig(baseURL: site, turnstileSiteKey: "key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [SessionBoundMCPURLProtocol.self]
        )
        return NativeChatMCPSessionBoundService(
            siteOrigin: site, app: .macos, apiClient: client, currentAccountId: account,
            legacyGrantCleanup: { cleanedAccounts.record($0) },
            protocolClasses: [SessionBoundMCPURLProtocol.self]
        )
    }

    final class CleanedAccounts: @unchecked Sendable {
        private let lock = NSLock()
        private var accounts: [String] = []

        func record(_ account: String) {
            lock.lock()
            accounts.append(account)
            lock.unlock()
        }

        var all: [String] {
            lock.lock()
            defer { lock.unlock() }
            return accounts
        }
    }

    private func toolNames(_ catalog: DecodedJSONValue) -> [String] {
        guard case let .object(result) = catalog, case let .array(tools)? = result["tools"] else { return [] }
        return tools.compactMap {
            if case let .object(tool) = $0, case let .string(name)? = tool["name"] { name } else { nil }
        }
    }

    func testConnectMintsOnceAndPublishesTheServerCatalogWithoutOAuthOrBrowser() async throws {
        SessionBoundMCPURLProtocol.reset()
        let service = makeService()

        let catalog = try await service.connect(accountId: "member-a")

        XCTAssertEqual(toolNames(catalog), ["server_tool"])
        XCTAssertEqual(SessionBoundMCPURLProtocol.mintCount, 1)
        XCTAssertTrue(SessionBoundMCPURLProtocol.mcpBearers.allSatisfy { $0 == "Bearer token-1" })
        XCTAssertFalse(SessionBoundMCPURLProtocol.paths.contains { $0.contains(".well-known") })
    }

    func testExplicitAuthorizationIgnoresTheScopeAndNeverOpensABrowser() async throws {
        SessionBoundMCPURLProtocol.reset()
        let service = makeService()

        let catalog = try await service.authorize(
            accountId: "member-a", scope: "mcp.user:write", presentationWindow: nil
        )

        XCTAssertEqual(toolNames(catalog), ["server_tool"])
        XCTAssertEqual(SessionBoundMCPURLProtocol.mintCount, 1)
    }

    func testRejectedBearerMintsOnceMoreThenTheCallSucceeds() async throws {
        SessionBoundMCPURLProtocol.reset(rejecting: ["Bearer token-1"])
        let service = makeService()

        _ = try await service.connect(accountId: "member-a")

        XCTAssertEqual(SessionBoundMCPURLProtocol.mintCount, 2)
        XCTAssertEqual(SessionBoundMCPURLProtocol.mcpBearers.last, "Bearer token-2")
    }

    func testSecondRejectionIsNotAuthorizedInsteadOfLooping() async throws {
        SessionBoundMCPURLProtocol.reset(rejecting: ["Bearer token-1", "Bearer token-2"])
        let service = makeService()

        do {
            _ = try await service.connect(accountId: "member-a")
            XCTFail("Expected the second rejection to surface")
        } catch MemberMCPTokenManager.Failure.notAuthorized {}
        XCTAssertEqual(SessionBoundMCPURLProtocol.mintCount, 2)
    }

    func testToolCallsUseTheMintedTokenAndRefuseAnotherAccount() async throws {
        SessionBoundMCPURLProtocol.reset()
        let service = makeService()
        _ = try await service.connect(accountId: "member-a")

        _ = try await service.callTool(accountId: "member-a", name: "server_tool", arguments: [:])

        XCTAssertEqual(SessionBoundMCPURLProtocol.mcpBearers.last, "Bearer token-1")
        do {
            _ = try await service.connect(accountId: "member-b")
            XCTFail("A live member context must not serve another account")
        } catch NativeChatMCPSessionBoundService.Failure.accountMismatch {}
    }

    func testAccountChangeBeforeMintDiscardsTheToken() async throws {
        SessionBoundMCPURLProtocol.reset()
        let service = makeService(account: { "member-b" })

        do {
            _ = try await service.connect(accountId: "member-a")
            XCTFail("A token for another session account must not be used")
        } catch MemberMCPTokenManager.Failure.notAuthorized {}
        XCTAssertTrue(SessionBoundMCPURLProtocol.mcpBearers.isEmpty)
    }

    func testDisconnectClosesWithTheLiveTokenNeverMintsAndRunsLegacyCleanup() async throws {
        SessionBoundMCPURLProtocol.reset()
        let cleaned = CleanedAccounts()
        let service = makeService(cleanedAccounts: cleaned)
        _ = try await service.connect(accountId: "member-a")

        try await service.disconnect(accountId: "member-a")

        XCTAssertEqual(SessionBoundMCPURLProtocol.mintCount, 1)
        XCTAssertEqual(SessionBoundMCPURLProtocol.paths.last, "DELETE /api/v1/mcp")
        XCTAssertEqual(SessionBoundMCPURLProtocol.mcpBearers.last, "Bearer token-1")
        XCTAssertEqual(cleaned.all, ["member-a"])
        do {
            _ = try await service.callTool(accountId: "member-a", name: "server_tool", arguments: [:])
            XCTFail("A disconnected account must not call tools")
        } catch NativeChatMCPSessionBoundService.Failure.accountMismatch {}
    }

    func testSignInConnectsTheAgentSessionAutomaticallyAndSignOutClearsIt() async throws {
        SessionBoundMCPURLProtocol.reset()
        let cleaned = CleanedAccounts()
        let session = NativeChatMCPAgentSession(service: makeService(cleanedAccounts: cleaned))

        await session.useAccount("member-a")
        try await session.connect()

        XCTAssertEqual(session.status, .connected)
        XCTAssertEqual(session.toolCatalog.map(toolNames), ["server_tool"])
        try await session.prepareForSignOut(accountId: "member-a")
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)
        XCTAssertEqual(cleaned.all, ["member-a"])
    }
}
