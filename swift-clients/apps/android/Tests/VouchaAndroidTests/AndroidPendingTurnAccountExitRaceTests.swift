import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnAccountExitRaceTests: XCTestCase {
    func testInFlightHistoryLoadBlocksAccountExitPreflight() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending()
        store.records[ownerA] = [pending.conversationID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let path = "/api/v1/my/conversations/\(pending.conversationID)/messages"
        fixture.recorder.enqueue(path: path, data: Data(), held: true)

        let history = Task { await viewModel.loadConversationMessages(reset: true) }
        _ = try await fixture.recorder.waitForRequest(path: path, occurrence: 2)
        let allowed = await viewModel.prepareForAccountExit()

        XCTAssertFalse(allowed)
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-a")
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertTrue(store.savedBodies.isEmpty)
        try fixture.recorder.completeHeld(
            path: path, occurrence: 2, statusCode: 200,
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )
        await history.value
    }

    func testAccountExitPreflightFencesNewHistoryAcknowledgement() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending()
        store.records[ownerA] = [pending.conversationID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        store.holdSave = true

        let preflight = Task { await viewModel.prepareForAccountExit() }
        await store.waitForSaveEntry()
        let path = "/api/v1/my/conversations/\(pending.conversationID)/messages"
        await viewModel.loadConversationMessages(reset: true)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: path).count, 1)
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        store.releaseSave()
        let allowed = await preflight.value

        XCTAssertTrue(allowed)
        XCTAssertEqual(store.records[ownerA]?[pending.conversationID], pending)
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        viewModel.cancelAccountExit()
    }

    private func makePending() -> AndroidPendingChatTurn {
        AndroidPendingChatTurn(
            conversationID: AndroidTranscriptTestFactory.newConversationID,
            userMessageID: "exit-user", assistantMessageID: "exit-assistant",
            userText: "Question", assistantText: "Answer",
            modelProvider: "android_aicore", modelName: nil
        )
    }
}
