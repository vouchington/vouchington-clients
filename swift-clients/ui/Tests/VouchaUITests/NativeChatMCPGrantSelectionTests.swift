import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPGrantSelectionTests: XCTestCase {
    func testExplicitConsentOpensBrowserWithSelectedScopeAndReloadsServerCatalog() async throws {
        MCPAgentURLProtocol.configure()
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        var scopes: [String] = []
        let service = try NativeChatMCPNetworkService(
            siteOrigin: site, app: .macos, tokenStore: HeldClearMCPTokenStore(holdClear: false),
            browserCode: { request, _ in
                let parts = try XCTUnwrap(URLComponents(url: request.authorizationURL, resolvingAgainstBaseURL: false))
                try scopes.append(XCTUnwrap(parts.queryItems?.first { $0.name == "scope" }?.value))
                return "new-code"
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect(scope: "mcp.user:read")
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        session.requestWriteAccess = true
        try await session.connect(scope: "mcp.user:read mcp.user:write")
        XCTAssertEqual(scopes, ["mcp.user:read", "mcp.user:read mcp.user:write"])
        XCTAssertFalse(session.isCurrent(snapshot))
        let encoder = JSONEncoder()
        encoder.outputFormatting = .sortedKeys
        XCTAssertEqual(try encoder.encode(session.toolCatalog), try encoder.encode(snapshot.catalog))
        XCTAssertTrue(session.requestWriteAccess)
        await session.useAccount("member-b")
        XCTAssertFalse(session.requestWriteAccess)
    }

    func testCancelledConsentRetainsSelectionAndReloadsCurrentGrantTools() async throws {
        MCPAgentURLProtocol.configure()
        var cancel = false
        let service = try NativeChatMCPNetworkService(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")), app: .macos,
            tokenStore: HeldClearMCPTokenStore(holdClear: false),
            browserCode: { _, _ in
                if cancel { throw CancellationError() }
                return "new-code"
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect()
        let snapshot = try XCTUnwrap(session.connectedSnapshot())
        session.requestWriteAccess = true
        cancel = true
        do {
            try await session.connect(scope: "mcp.user:read mcp.user:write")
            XCTFail("Cancellation must propagate")
        } catch is CancellationError {}
        XCTAssertTrue(session.requestWriteAccess)
        XCTAssertNotNil(session.toolCatalog)
        XCTAssertEqual(session.status, .connected)
        XCTAssertFalse(session.isCurrent(snapshot))
        cancel = false
        try await session.connect(scope: "mcp.user:read mcp.user:write")
        XCTAssertEqual(session.status, .connected)
    }

    func testWriteSelectionOffersNativePlansNavigationWithoutPlanClassification() async throws {
        MCPAgentURLProtocol.configure()
        let service = try NativeChatMCPNetworkService(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")), app: .macos,
            tokenStore: HeldClearMCPTokenStore(holdClear: false), browserCode: { _, _ in "code" },
            protocolClasses: [MCPAgentURLProtocol.self]
        )
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        session.requestWriteAccess = true
        var destination: String?
        let surface = NativeChatSurface(client: nil, routeMatch: nil, currentUserId: "member-a", mcpSession: session) {
            destination = $0
        }
        let button = try surface.mcpControls().inspect()
            .find(button: uiEnglish(.nativeSwiftTagManagementViewPlans))
        try button.tap()
        XCTAssertEqual(destination, "/plans")
        XCTAssertTrue(session.requestWriteAccess)
    }

    func testAccountSwitchCannotPublishDelayedConsentOrRestoreOldTools() async throws {
        MCPAgentURLProtocol.configure()
        var calls = 0
        var callback: CheckedContinuation<String, Never>?
        let started = expectation(description: "Second browser consent started")
        let service = try NativeChatMCPNetworkService(
            siteOrigin: XCTUnwrap(URL(string: "https://example.test")), app: .macos,
            tokenStore: HeldClearMCPTokenStore(holdClear: false), browserCode: { _, _ in
                calls += 1
                if calls == 1 { return "first-code" }
                return await withCheckedContinuation {
                    callback = $0
                    started.fulfill()
                }
            }, protocolClasses: [MCPAgentURLProtocol.self]
        )
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        try await session.connect(scope: "mcp.user:read")
        let consent = Task { try await session.connect(scope: "mcp.user:read mcp.user:write") }
        await fulfillment(of: [started], timeout: 2)
        await session.useAccount("member-b")
        callback?.resume(returning: "late-code")
        try await consent.value
        XCTAssertEqual(session.accountId, "member-b")
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)
    }

}
