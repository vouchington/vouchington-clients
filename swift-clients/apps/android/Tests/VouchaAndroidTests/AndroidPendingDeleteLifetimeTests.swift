import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidPendingDeleteLifetimeTests: XCTestCase {
    func testHeldAcknowledgementForOldConversationCannotReplaceNewSelection() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000002"
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: fixture.recorder, firstID: firstID, secondID: secondID
        )
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending(for: firstID)
        let owner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        store.records[owner] = [firstID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        store.holdNextDelete = true
        try fixture.recorder.enqueue(
            path: historyPath(firstID), data: canonicalHistory(for: pending)
        )

        let oldLoad = Task { await viewModel.loadConversationMessages(reset: true) }
        await store.waitForDeleteEntry()
        await viewModel.selectConversation(secondID)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Second conversation history"])
        store.releaseDelete()
        await oldLoad.value

        XCTAssertEqual(viewModel.selectedConversationID, secondID)
        XCTAssertEqual(viewModel.loadedConversationHistoryID, secondID)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Second conversation history"])
        XCTAssertNil(viewModel.pendingTurns[firstID])
        XCTAssertNil(store.records[owner]?[firstID])
    }

    func testHeldAcknowledgementForOldAccountCannotReplaceNewAccountHistory() async throws {
        let first = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: first.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: first.recorder)
        let second = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: second.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        second.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(id: secondID, title: "Other account")
        )
        second.recorder.enqueue(
            path: historyPath(secondID),
            data: AndroidTranscriptTestFactory.messagePageResponse(
                id: "other-account-message", conversationID: secondID, content: "Other account history"
            )
        )
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending(for: firstID)
        let owner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: first.client)
        store.records[owner] = [firstID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: first.client, userID: "owner-a")
        store.holdNextDelete = true
        try first.recorder.enqueue(path: historyPath(firstID), data: canonicalHistory(for: pending))

        let oldLoad = Task { await viewModel.loadConversationMessages(reset: true) }
        await store.waitForDeleteEntry()
        XCTAssertEqual(
            viewModel.pendingTurnHistoryAcknowledgements[firstID],
            Set([pending.userMessageID, pending.assistantMessageID])
        )
        await viewModel.bindAccount(client: second.client, userID: "owner-b")
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-b")
        XCTAssertTrue(store.saveAttempts.isEmpty)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Other account history"])
        store.releaseDelete()
        await oldLoad.value

        XCTAssertEqual(viewModel.currentAccountUserID, "owner-b")
        XCTAssertEqual(viewModel.selectedConversationID, secondID)
        XCTAssertEqual(viewModel.loadedConversationHistoryID, secondID)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Other account history"])
    }

    func testHeldAcknowledgementCompletesBeforeReboundSameOwnerLoad() async throws {
        let first = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: first.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: first.recorder)
        let second = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: second.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: second.recorder)
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending(for: conversationID)
        let owner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: first.client)
        store.records[owner] = [conversationID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: first.client, userID: "owner-a")
        store.holdNextDelete = true
        try first.recorder.enqueue(
            path: historyPath(conversationID), data: canonicalHistory(for: pending)
        )

        let oldLoad = Task { await viewModel.loadConversationMessages(reset: true) }
        await store.waitForDeleteEntry()
        XCTAssertEqual(
            viewModel.pendingTurnHistoryAcknowledgements[conversationID],
            Set([pending.userMessageID, pending.assistantMessageID])
        )
        await viewModel.bindAccount(client: second.client, userID: "owner-b")
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-b")
        XCTAssertTrue(store.saveAttempts.isEmpty)
        first.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(id: conversationID, title: "First")
        )
        first.recorder.enqueue(
            path: historyPath(conversationID), data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        let rebound = Task { await viewModel.bindAccount(client: first.client, userID: "owner-a") }
        store.releaseDelete()
        await oldLoad.value
        await rebound.value

        XCTAssertEqual(viewModel.currentAccountUserID, "owner-a")
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertTrue(store.saveAttempts.isEmpty)
    }

    private func makePending(for conversationID: String) -> AndroidPendingChatTurn {
        AndroidPendingChatTurn(
            conversationID: conversationID, userMessageID: "pending-user", assistantMessageID: "pending-assistant",
            userText: "Question", assistantText: "Answer", modelProvider: "android_aicore", modelName: nil
        )
    }

    private func historyPath(_ conversationID: String) -> String {
        "/api/v1/my/conversations/\(conversationID)/messages"
    }

    private func canonicalHistory(for pending: AndroidPendingChatTurn) throws -> Data {
        try historyPage([
            serverMessage(
                id: pending.userMessageID, conversationID: pending.conversationID,
                role: "user", text: "Canonical question"
            ),
            serverMessage(
                id: pending.assistantMessageID, conversationID: pending.conversationID,
                role: "assistant", text: "Canonical answer"
            )
        ], hasNextPage: false, cursor: nil)
    }
}
