import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnDiscardRaceTests: XCTestCase {
    func testStaleConfirmationCannotDiscardNewAccountsUnreadableRecords() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let first = makeRacePending("first")
        let other = makeRacePending("other")
        store.records = [
            ownerA: [first.conversationID: first],
            ownerB: [other.conversationID: other]
        ]
        store.failLoad = true
        let viewModel = makeRaceViewModel(store)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let staleConfirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        await viewModel.bindAccount(client: fixture.client, userID: "owner-b")

        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        let discarded = await viewModel.discardPendingTurns(after: staleConfirmation)
        XCTAssertFalse(discarded)
        XCTAssertTrue(store.discardCalls.isEmpty)
        XCTAssertEqual(store.records[ownerA]?[first.conversationID], first)
        XCTAssertEqual(store.records[ownerB]?[other.conversationID], other)
    }

    func testHeldDiscardCannotClearNewAccountsRecoveryState() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let first = makeRacePending("first")
        let other = makeRacePending("other")
        store.records = [
            ownerA: [first.conversationID: first],
            ownerB: [other.conversationID: other]
        ]
        store.failLoad = true
        store.holdNextDiscard = true
        let viewModel = makeRaceViewModel(store)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())

        let discard = Task { await viewModel.discardPendingTurns(after: confirmation) }
        await store.waitForDiscardEntry()
        XCTAssertTrue(viewModel.pendingStoreIsLoading)
        let exitAllowed = await viewModel.prepareForAccountExit()
        XCTAssertFalse(exitAllowed)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-b")
        store.releaseDiscard()

        let discarded = await discard.value
        XCTAssertFalse(discarded)
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-b")
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        XCTAssertEqual(store.discardCalls, [ownerA])
        XCTAssertNil(store.records[ownerA])
        XCTAssertEqual(store.records[ownerB]?[other.conversationID], other)
    }

}

@MainActor
private func makeRaceViewModel(_ store: AndroidPendingTurnStoreTestDouble) -> ViewModel {
    AndroidTranscriptTestFactory.makeViewModel(
        runtime: FakeAICoreRuntime(state: .available), pendingStore: store
    )
}

private func makeRacePending(_ suffix: String) -> AndroidPendingChatTurn {
    AndroidPendingChatTurn(
        conversationID: "conversation-\(suffix)",
        userMessageID: "user-\(suffix)", assistantMessageID: "assistant-\(suffix)",
        userText: "Question", assistantText: "Answer",
        modelProvider: "android_aicore", modelName: nil
    )
}
