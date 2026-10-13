import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPAgentSessionTests: XCTestCase {
    func testAccountSwitchDiscardsDelayedToolsAndRevokesPreviousGrant() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()

        await session.useAccount("member-b")
        XCTAssertEqual(service.disconnectedAccounts, ["member-a"])
        XCTAssertEqual(session.accountId, "member-b")
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)

        service.finishConnect(with: .object(["tools": .array([.string("stale")])]))
        try await connect.value
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)
    }

    func testSignOutDiscardsDelayedToolsAndClearsAccount() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()

        await session.useAccount(nil)
        service.finishConnect(with: .object(["tools": .array([.string("stale")])]))
        try await connect.value

        XCTAssertNil(session.accountId)
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)
        XCTAssertEqual(service.disconnectedAccounts, ["member-a"])
    }

    func testConnectAndRefreshPublishCurrentCatalog() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([.string("first")])]))
        try await connect.value
        XCTAssertEqual(session.status, .connected)
        XCTAssertEqual(service.connectedAccounts, ["member-a"])

        try await session.refreshTools()
        XCTAssertEqual(service.refreshedAccounts, ["member-a"])
        guard case let .object(result)? = session.toolCatalog,
              case let .array(tools)? = result["tools"],
              case let .string(name)? = tools.first
        else { return XCTFail("Expected refreshed tool catalog") }
        XCTAssertEqual(name, "refreshed")

        try await session.disconnect()
        XCTAssertEqual(session.accountId, "member-a")
        XCTAssertEqual(session.status, .disconnected)
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(service.disconnectedAccounts, ["member-a"])
    }

    func testFailedRefreshInvalidatesCatalog() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([])]))
        try await connect.value

        service.failRefresh = true
        do {
            try await session.refreshTools()
            XCTFail("Expected catalog refresh to fail")
        } catch SuspendedMCPService.Failure.refreshFailed {}

        XCTAssertEqual(session.status, .failed)
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(service.refreshedAccounts, ["member-a"])
    }

    func testFailedDisconnectKeepsCatalogInvalidated() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([])]))
        try await connect.value

        service.failDisconnect = true
        do {
            try await session.disconnect()
            XCTFail("Expected grant cleanup to fail")
        } catch SuspendedMCPService.Failure.disconnectFailed {}

        XCTAssertEqual(session.status, .failed)
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(service.disconnectedAccounts, ["member-a"])
    }

    func testChatControlsRenderAuthorizationAndConnectedActions() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let chat = NativeChatSurface(
            client: nil, routeMatch: nil, currentUserId: "member-a", mcpSession: session
        )
        let disconnected = try chat.mcpControls().inspect()
        XCTAssertNoThrow(try disconnected.find(text: uiEnglish(.nativeSwiftChatMcpAuthorizationRequired)))
        XCTAssertNoThrow(try disconnected.find(button: uiEnglish(.nativeSwiftChatMcpConnect)))

        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([])]))
        try await connect.value

        let connected = try chat.mcpControls().inspect()
        XCTAssertNoThrow(try connected.find(text: uiEnglish(.nativeSwiftChatMcpConnected)))
        XCTAssertNoThrow(try connected.find(button: uiEnglish(.nativeSwiftChatMcpRefresh)))
        XCTAssertNoThrow(try connected.find(button: uiEnglish(.nativeSwiftChatMcpDisconnect)))
    }
}

@MainActor
private final class SuspendedMCPService: NativeChatMCPServicing {
    enum Failure: Error { case refreshFailed, disconnectFailed }
    private var pendingConnect: CheckedContinuation<DecodedJSONValue, Error>?
    private var started: CheckedContinuation<Void, Never>?
    private var hasStarted = false
    private(set) var connectedAccounts: [String] = []
    private(set) var refreshedAccounts: [String] = []
    private(set) var disconnectedAccounts: [String] = []
    var failRefresh = false
    var failDisconnect = false

    func connect(accountId: String) async throws -> DecodedJSONValue {
        connectedAccounts.append(accountId)
        hasStarted = true
        started?.resume()
        started = nil
        return try await withCheckedThrowingContinuation { pendingConnect = $0 }
    }

    func waitForConnect() async {
        if hasStarted { return }
        await withCheckedContinuation { started = $0 }
    }

    func finishConnect(with catalog: DecodedJSONValue) {
        pendingConnect?.resume(returning: catalog)
        pendingConnect = nil
    }

    func refreshTools(accountId: String) async throws -> DecodedJSONValue {
        refreshedAccounts.append(accountId)
        if failRefresh { throw Failure.refreshFailed }
        return .object(["tools": .array([.string("refreshed")])])
    }

    func disconnect(accountId: String) async throws {
        disconnectedAccounts.append(accountId)
        if failDisconnect { throw Failure.disconnectFailed }
    }
}
