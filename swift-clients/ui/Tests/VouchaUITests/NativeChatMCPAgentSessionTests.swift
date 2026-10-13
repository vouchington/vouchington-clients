import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeChatMCPAgentSessionTests: XCTestCase {
    func testSignOutBeforeChatConnectionClearsKnownAccount() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)

        try await session.prepareForSignOut(accountId: "member-a")

        XCTAssertEqual(service.disconnectedAccounts, ["member-a"])
        XCTAssertNil(session.accountId)
        XCTAssertNil(session.toolCatalog)
        XCTAssertEqual(session.status, .disconnected)
    }

    func testPrepareForSignOutCancelsInFlightToolCall() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([])]))
        try await connect.value
        let snapshot = try XCTUnwrap(session.connectedSnapshot())

        let cancelled = expectation(description: "In-flight member tool call cancelled")
        service.toolCallCancellationExpectation = cancelled
        let call = Task {
            try await session.callTool(
                name: "read_my_profile", arguments: [:],
                accountId: snapshot.accountId, revision: snapshot.revision
            )
        }
        await service.waitForToolCall()

        try await session.prepareForSignOut(accountId: "member-a")
        await fulfillment(of: [cancelled], timeout: 2)
        XCTAssertFalse(session.isCurrent(snapshot))
        try service.finishToolCall()
        do {
            _ = try await call.value
            XCTFail("Former member tool call must not publish after sign-out")
        } catch is CancellationError {}
    }

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
        XCTAssertNotNil(session.lastError)
        XCTAssertEqual(service.refreshedAccounts, ["member-a"])
    }

    func testRefreshDoesNotExposeOldCatalogUnderNewRevision() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([.string("old")])]))
        try await connect.value
        let previous = try XCTUnwrap(session.connectedSnapshot())

        service.suspendRefresh = true
        let refresh = Task { try await session.refreshTools() }
        await service.waitForRefresh()
        XCTAssertNil(session.connectedSnapshot(), "Old schema must not be captured during refresh")
        XCTAssertFalse(session.isCurrent(previous))

        service.finishRefresh(with: .object(["tools": .array([.string("new")])]))
        try await refresh.value
        let current = try XCTUnwrap(session.connectedSnapshot())
        XCTAssertNotEqual(current.revision, previous.revision)
        guard case let .object(catalog) = current.catalog,
              case let .array(tools)? = catalog["tools"]
        else { return XCTFail("Expected refreshed catalog") }
        guard case let .string(name)? = tools.first else { return XCTFail("Expected replacement tool") }
        XCTAssertEqual(name, "new")
        XCTAssertFalse(session.isCurrent(previous))
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

    func testFailedSignOutMustClearFormerAccountBeforeAnotherMemberConnects() async throws {
        let service = SuspendedMCPService()
        let session = NativeChatMCPAgentSession(service: service)
        await session.useAccount("member-a")
        service.failDisconnect = true

        await session.useAccount(nil)
        XCTAssertEqual(session.status, .failed)
        XCTAssertNotNil(session.lastError)
        await session.useAccount("member-b")
        XCTAssertEqual(session.status, .failed)
        do {
            try await session.connect()
            XCTFail("A retained former grant must block another member's connection")
        } catch SuspendedMCPService.Failure.disconnectFailed {}
        XCTAssertTrue(service.connectedAccounts.isEmpty)

        service.failDisconnect = false
        await session.useAccount("member-b")
        XCTAssertEqual(session.status, .disconnected)
        let connect = Task { try await session.connect() }
        await service.waitForConnect()
        service.finishConnect(with: .object(["tools": .array([])]))
        try await connect.value
        XCTAssertEqual(service.connectedAccounts, ["member-b"])
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
        service.finishConnect(with: .object(["tools": .array([
            .object(["name": .string("read_post")])
        ])]))
        try await connect.value

        let connected = try chat.mcpControls().inspect()
        XCTAssertNoThrow(try connected.find(text: uiEnglish(.nativeSwiftChatMcpConnected)))
        XCTAssertNoThrow(try connected.find(button: uiEnglish(.nativeSwiftChatMcpRefresh)))
        XCTAssertNoThrow(try connected.find(button: uiEnglish(.nativeSwiftChatMcpDisconnect)))
        XCTAssertNoThrow(try connected.find(text: "read_post"))
    }

    func testMessagesDirectoryChatDestinationKeepsMemberMcpControls() async throws {
        let session = NativeChatMCPAgentSession(service: SuspendedMCPService())
        await session.useAccount("member-a")
        let directory = NativeRouteFamilyDirectoryView(
            groups: [AppSection.messages.nativeParityGroups[1]], isSignedIn: true,
            currentUserId: "member-a", nativeChatMCPSession: session
        )

        let chat = try directory.inspect().find(NativeChatSurface.self).actualView()
        XCTAssertNoThrow(
            try chat.mcpControls().inspect().find(button: uiEnglish(.nativeSwiftChatMcpConnect))
        )
    }
}

@MainActor
private final class SuspendedMCPService: NativeChatMCPServicing {
    enum Failure: Error { case refreshFailed, disconnectFailed }
    private var pendingConnect: CheckedContinuation<DecodedJSONValue, Error>?
    private var started: CheckedContinuation<Void, Never>?
    private var hasStarted = false
    private var pendingToolCall: CheckedContinuation<MemberMCPClient.ToolResult, Error>?
    private var toolCallStarted: CheckedContinuation<Void, Never>?
    private var hasToolCallStarted = false
    var toolCallCancellationExpectation: XCTestExpectation?
    private var pendingRefresh: CheckedContinuation<DecodedJSONValue, Error>?
    private var refreshStarted: CheckedContinuation<Void, Never>?
    private var hasRefreshStarted = false
    var suspendRefresh = false
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

    func callTool(
        accountId _: String, name _: String, arguments _: [String: DecodedJSONValue]
    ) async throws -> MemberMCPClient.ToolResult {
        hasToolCallStarted = true
        toolCallStarted?.resume()
        toolCallStarted = nil
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { pendingToolCall = $0 }
        } onCancel: {
            Task { @MainActor in self.toolCallCancellationExpectation?.fulfill() }
        }
    }

    func waitForToolCall() async {
        if hasToolCallStarted { return }
        await withCheckedContinuation { toolCallStarted = $0 }
    }

    func finishToolCall() throws {
        let result = try JSONDecoder().decode(MemberMCPClient.ToolResult.self, from: Data(#"{"content":[]}"#.utf8))
        pendingToolCall?.resume(returning: result)
        pendingToolCall = nil
    }

    func refreshTools(accountId: String) async throws -> DecodedJSONValue {
        refreshedAccounts.append(accountId)
        if failRefresh { throw Failure.refreshFailed }
        if suspendRefresh {
            return try await withCheckedThrowingContinuation { continuation in
                pendingRefresh = continuation
                hasRefreshStarted = true
                refreshStarted?.resume()
                refreshStarted = nil
            }
        }
        return .object(["tools": .array([.string("refreshed")])])
    }

    func waitForRefresh() async {
        if hasRefreshStarted { return }
        await withCheckedContinuation { refreshStarted = $0 }
    }

    func finishRefresh(with catalog: DecodedJSONValue) {
        pendingRefresh?.resume(returning: catalog)
        pendingRefresh = nil
    }

    func disconnect(accountId: String) async throws {
        disconnectedAccounts.append(accountId)
        if failDisconnect { throw Failure.disconnectFailed }
    }
}
