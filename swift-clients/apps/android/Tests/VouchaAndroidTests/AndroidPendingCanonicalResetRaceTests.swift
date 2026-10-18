import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingCanonicalResetRaceTests: XCTestCase {
    func testResetRetryRetainsSameIDsReplacedByCanonicalPersistence() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        let savePath = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(id: conversationID, title: "Chat")
        )
        fixture.recorder.enqueue(path: historyPath, statusCode: 503, data: Data("{}".utf8))
        fixture.recorder.enqueue(
            path: historyPath,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse(),
            held: true
        )
        fixture.recorder.enqueue(
            path: savePath, held: true,
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: conversationID, userText: "Saved question", assistantText: "Saved answer"
            )
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let pending = makePendingTurn(conversationID: conversationID)
        viewModel.pendingTurns[conversationID] = pending
        viewModel.appendPendingTurnIfNeeded(for: conversationID)
        let save = Task { await viewModel.retryPendingTurn() }
        _ = try await fixture.recorder.waitForRequest(path: savePath, occurrence: 1)
        let history = Task { await viewModel.retryConversationLoad() }
        _ = try await fixture.recorder.waitForRequest(path: historyPath, occurrence: 2)
        fixture.recorder.completeHeld(
            path: savePath, occurrence: 1, statusCode: 200,
            data: AndroidTranscriptTestFactory.generatedChatResponse(
                conversationID: conversationID, userText: pending.userText, assistantText: pending.assistantText
            )
        )
        await save.value
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
        fixture.recorder.completeHeld(
            path: historyPath, occurrence: 2, statusCode: 200,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await history.value
        XCTAssertEqual(viewModel.messages.map(\.id), [pending.userMessageID, pending.assistantMessageID])
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
        XCTAssertNil(viewModel.failedConversationHistoryLoad)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: savePath).count, 1)
    }

    private func makePendingTurn(conversationID: String) -> AndroidPendingChatTurn {
        AndroidPendingChatTurn(
            conversationID: conversationID,
            userMessageID: AndroidTranscriptTestFactory.generatedUserMessageID,
            assistantMessageID: AndroidTranscriptTestFactory.generatedAssistantMessageID,
            userText: "Saved question", assistantText: "Saved answer",
            modelProvider: "android_aicore", modelName: "android-aicore-system"
        )
    }
}
