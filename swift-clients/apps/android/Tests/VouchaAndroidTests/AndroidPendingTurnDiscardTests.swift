import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnDiscardTests: XCTestCase {
    func testConfirmedDiscardRemovesOnlyCurrentOwnersRecordsAndKey() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let first = makePending("first")
        let second = makePending("second")
        let other = makePending("other")
        store.records = [
            ownerA: [first.conversationID: first, second.conversationID: second],
            ownerB: [other.conversationID: other]
        ]
        store.ownerKeys = [ownerA, ownerB]
        store.failLoad = true
        let viewModel = makeViewModel(store)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())

        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        XCTAssertTrue(store.discardCalls.isEmpty) // Capturing a token alone does not delete data.
        XCTAssertEqual(store.records[ownerA]?.count, 2)

        let discarded = await viewModel.discardPendingTurns(after: confirmation)

        XCTAssertTrue(discarded)
        XCTAssertFalse(viewModel.pendingStoreLoadFailed)
        XCTAssertFalse(viewModel.pendingStoreDiscardFailed)
        XCTAssertEqual(store.discardCalls, [ownerA])
        XCTAssertNil(store.records[ownerA])
        XCTAssertFalse(store.ownerKeys.contains(ownerA))
        XCTAssertFalse(store.discardMarkers.contains(ownerA))
        XCTAssertEqual(store.records[ownerB]?[other.conversationID], other)
        XCTAssertTrue(store.ownerKeys.contains(ownerB))
    }

    func testPartialDeleteFailureStaysBlockedAndRetryFinishesWithoutTouchingOtherOwner() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let first = makePending("first")
        let second = makePending("second")
        let other = makePending("other")
        store.records = [
            ownerA: [first.conversationID: first, second.conversationID: second],
            ownerB: [other.conversationID: other]
        ]
        store.ownerKeys = [ownerA, ownerB]
        store.failLoad = true
        let viewModel = makeViewModel(store)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        store.failDiscardAfterPartialDelete = true

        let firstDiscard = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertFalse(firstDiscard)
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        XCTAssertTrue(viewModel.pendingStoreDiscardFailed)
        XCTAssertEqual(store.records[ownerA]?.count, 1)
        XCTAssertTrue(store.ownerKeys.contains(ownerA))
        XCTAssertTrue(store.discardMarkers.contains(ownerA))
        XCTAssertEqual(store.records[ownerB]?[other.conversationID], other)

        store.failLoad = false
        let loadCalls = store.loadCalls
        await viewModel.retryPendingTurnLoad()
        XCTAssertEqual(store.loadCalls, loadCalls)
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        XCTAssertTrue(viewModel.pendingStoreDiscardFailed)

        store.failDiscardAfterPartialDelete = false
        let retriedDiscard = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertTrue(retriedDiscard)
        XCTAssertFalse(viewModel.pendingStoreLoadFailed)
        XCTAssertNil(store.records[ownerA])
        XCTAssertFalse(store.ownerKeys.contains(ownerA))
        XCTAssertFalse(store.discardMarkers.contains(ownerA))
        XCTAssertEqual(store.records[ownerB]?[other.conversationID], other)
        XCTAssertTrue(store.ownerKeys.contains(ownerB))
    }

    func testKeyDeleteFailureRemainsBlockedAndCanBeRetried() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = makePending("first")
        store.records[ownerA] = [pending.conversationID: pending]
        store.ownerKeys = [ownerA]
        store.failLoad = true
        let viewModel = makeViewModel(store)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        store.failDiscardKeyDeletion = true

        let firstDiscard = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertFalse(firstDiscard)
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        XCTAssertTrue(viewModel.pendingStoreDiscardFailed)
        XCTAssertNil(store.records[ownerA])
        XCTAssertTrue(store.ownerKeys.contains(ownerA))
        XCTAssertTrue(store.discardMarkers.contains(ownerA))

        store.failLoad = false
        let loadCalls = store.loadCalls
        await viewModel.retryPendingTurnLoad()
        XCTAssertEqual(store.loadCalls, loadCalls)
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)

        await viewModel.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        XCTAssertTrue(viewModel.pendingStoreLoadFailed)
        let resumedConfirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        let staleDiscard = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertFalse(staleDiscard)
        XCTAssertTrue(store.ownerKeys.contains(ownerA))

        store.failDiscardKeyDeletion = false
        let retriedDiscard = await viewModel.discardPendingTurns(after: resumedConfirmation)
        XCTAssertTrue(retriedDiscard)
        XCTAssertFalse(store.ownerKeys.contains(ownerA))
        XCTAssertFalse(store.discardMarkers.contains(ownerA))
        XCTAssertFalse(viewModel.pendingStoreLoadFailed)
    }
}

@MainActor
private func makeViewModel(_ store: AndroidPendingTurnStoreTestDouble) -> ViewModel {
    AndroidTranscriptTestFactory.makeViewModel(
        runtime: FakeAICoreRuntime(state: .available), pendingStore: store
    )
}

private func makePending(_ suffix: String) -> AndroidPendingChatTurn {
    AndroidPendingChatTurn(
        conversationID: "conversation-\(suffix)",
        userMessageID: "user-\(suffix)", assistantMessageID: "assistant-\(suffix)",
        userText: "Question", assistantText: "Answer",
        modelProvider: "android_aicore", modelName: nil
    )
}
