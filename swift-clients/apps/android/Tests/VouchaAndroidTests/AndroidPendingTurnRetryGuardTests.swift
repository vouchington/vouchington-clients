import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnRetryGuardTests: XCTestCase {
    func testPendingTurnRetryWaitsForConversationLoadingAndCreation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let path = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        let pending = AndroidPendingChatTurn(
            conversationID: conversationID,
            userMessageID: "retry-user-message",
            assistantMessageID: "retry-assistant-message",
            userText: "Persist this exact turn",
            assistantText: "The already generated answer",
            modelProvider: "android_aicore",
            modelName: "android-aicore-system"
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        viewModel.apiClient = fixture.client
        viewModel.currentAccountUserID = "fixture-user"
        viewModel.selectedConversationID = conversationID
        viewModel.pendingTurns[conversationID] = pending

        viewModel.isLoadingConversations = true
        await viewModel.retryPendingTurn()
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)

        viewModel.isLoadingConversations = false
        viewModel.isCreatingConversation = true
        await viewModel.retryPendingTurn()
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)

        viewModel.isCreatingConversation = false
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            conversationID: conversationID,
            userText: pending.userText,
            assistantText: pending.assistantText
        )
        await viewModel.retryPendingTurn()

        let requests = fixture.recorder.requestSnapshot(path: path)
        XCTAssertEqual(requests.count, 1)
        let body = try XCTUnwrap(requests[0].httpBody).jsonObject()
        XCTAssertEqual(body["user_message_id"] as? String, pending.userMessageID)
        XCTAssertEqual(body["assistant_message_id"] as? String, pending.assistantMessageID)
        XCTAssertEqual(body["message"] as? String, pending.userText)
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
    }

}
