import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidPendingDeleteRecoveryTests: XCTestCase {
    func testFailedCanonicalDeleteRemainsRecoverableWithoutResavingOrPosting() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let pending = AndroidPendingChatTurn(
            conversationID: conversationID, userMessageID: "pending-user", assistantMessageID: "pending-assistant",
            userText: "Question", assistantText: "Answer", modelProvider: "android_aicore", modelName: nil
        )
        let owner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let store = AndroidPendingTurnStoreTestDouble()
        store.records[owner] = [conversationID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        store.failDelete = true
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        let canonicalHistory = try historyPage([
            serverMessage(
                id: pending.userMessageID, conversationID: conversationID,
                role: "user", text: "Canonical question"
            ),
            serverMessage(
                id: pending.assistantMessageID, conversationID: conversationID,
                role: "assistant", text: "Canonical answer"
            )
        ], hasNextPage: false, cursor: nil)
        fixture.recorder.enqueue(path: historyPath, data: canonicalHistory)
        await viewModel.loadConversationMessages(reset: true)
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertEqual(
            viewModel.pendingTurnHistoryAcknowledgements[conversationID],
            Set([pending.userMessageID, pending.assistantMessageID])
        )

        await viewModel.bindAccount(client: nil, userID: nil)
        XCTAssertTrue(store.saveAttempts.isEmpty)
        XCTAssertEqual(store.records[owner]?[conversationID], pending)
        store.failDelete = false
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(id: conversationID, title: "First")
        )
        fixture.recorder.enqueue(path: historyPath, data: canonicalHistory)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")

        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertNil(store.records[owner]?[conversationID])
        XCTAssertTrue(store.saveAttempts.isEmpty)
        let postPath = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
    }
}
