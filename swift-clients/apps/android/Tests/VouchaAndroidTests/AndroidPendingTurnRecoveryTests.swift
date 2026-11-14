import Foundation
@testable import VouchaAndroid
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidPendingTurnRecoveryTests: XCTestCase {
    func testPersistenceFailureErrorReturnsWithPendingTurnAfterConversationSwitch() async throws {
        let setup = try await makeConversationSwitchSetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let send = Task { await setup.viewModel.sendOnDevice() }
        _ = try await setup.fixture.recorder.waitForRequest(path: setup.sendPath, occurrence: 1)
        await setup.viewModel.selectConversation(setup.secondID)
        try setup.fixture.recorder.completeHeld(
            path: setup.sendPath,
            occurrence: 1,
            statusCode: 503,
            data: Data(#"{"message":"offline"}"#.utf8)
        )
        await send.value

        XCTAssertNotNil(setup.viewModel.pendingTurns[setup.firstID])
        XCTAssertNil(setup.viewModel.chatErrorMessageKey)
        await setup.viewModel.selectConversation(setup.firstID)

        XCTAssertNotNil(setup.viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        await setup.viewModel.retryPendingTurn()

        XCTAssertNil(setup.viewModel.pendingTurnForSelectedConversation)
        let posts = setup.fixture.recorder.requestSnapshot(path: setup.sendPath)
        XCTAssertEqual(posts.count, 2)
        XCTAssertEqual(
            try canonicalJSONBody(posts[0].httpBody),
            try canonicalJSONBody(posts[1].httpBody)
        )
    }

    func testRawHistoryAcknowledgesPendingTurnAcrossPagesOnlyWithBothRoles() async throws {
        let setup = try await makePendingHistorySetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let pending = setup.pending

        try setup.fixture.recorder.enqueue(
            path: setup.historyPath,
            data: historyPage([
                serverMessage(
                    id: pending.userMessageID,
                    conversationID: setup.conversationID,
                    role: "user",
                    text: "Question"
                )
            ], hasNextPage: true, cursor: "older")
        )
        await setup.viewModel.loadConversationMessages(reset: true)
        XCTAssertNotNil(setup.viewModel.pendingTurnForSelectedConversation)

        try setup.fixture.recorder.enqueue(
            path: setup.historyPath,
            data: historyPage([
                serverMessage(
                    id: pending.userMessageID,
                    conversationID: setup.conversationID,
                    role: "assistant",
                    text: "Wrong role"
                ),
                serverMessage(
                    id: pending.assistantMessageID,
                    conversationID: "another-conversation",
                    role: "assistant",
                    text: "Wrong conversation"
                )
            ], hasNextPage: true, cursor: "oldest")
        )
        await setup.viewModel.loadConversationMessages(reset: false)
        XCTAssertNotNil(setup.viewModel.pendingTurnForSelectedConversation)

        try setup.fixture.recorder.enqueue(
            path: setup.historyPath,
            data: historyPage([
                serverMessage(
                    id: pending.assistantMessageID,
                    conversationID: setup.conversationID,
                    role: "assistant",
                    text: "Canonical answer"
                )
            ], hasNextPage: false, cursor: nil)
        )
        await setup.viewModel.loadConversationMessages(reset: false)

        XCTAssertNil(setup.viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(setup.viewModel.messages.map(\.id), [pending.userMessageID, pending.assistantMessageID])
        XCTAssertEqual(setup.viewModel.messages.last?.content, "Canonical answer")
    }
}
