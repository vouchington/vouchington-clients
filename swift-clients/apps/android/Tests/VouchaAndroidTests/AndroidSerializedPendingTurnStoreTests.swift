import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidSerializedPendingTurnStoreTests: XCTestCase {
    func testOwnerDiscardWaitsForEarlierSaveWhileOtherOwnerCanLoad() async throws {
        let backing = AndroidPendingTurnStoreTestDouble()
        let store = AndroidSerializedPendingTurnStore(backing)
        let turnA = pending("a")
        let turnB = pending("b")
        backing.records["owner-b"] = [turnB.conversationID: turnB]
        backing.holdSave = true
        let saveA = Task { try await store.save(ownerID: "owner-a", turn: turnA) }
        await backing.waitForSaveEntry()

        let discardStarted = expectation(description: "owner A discard requested")
        let discardA = Task {
            discardStarted.fulfill()
            try await store.discardOwner(ownerID: "owner-a")
        }
        await fulfillment(of: [discardStarted], timeout: 3)
        let loadedB = try await store.load(ownerID: "owner-b")
        XCTAssertEqual(loadedB, [turnB])
        XCTAssertTrue(backing.discardCalls.isEmpty)

        backing.releaseSave()
        try await saveA.value
        try await discardA.value
        XCTAssertEqual(backing.discardCalls, ["owner-a"])
        XCTAssertNil(backing.records["owner-a"])
        XCTAssertEqual(backing.records["owner-b"]?[turnB.conversationID], turnB)
    }

    func testFailedSaveReleasesOwnerQueueForConfirmedDiscard() async throws {
        let backing = AndroidPendingTurnStoreTestDouble()
        let store = AndroidSerializedPendingTurnStore(backing)
        backing.failSave = true
        do {
            try await store.save(ownerID: "owner-a", turn: pending("a"))
            XCTFail("Expected the encrypted save to fail")
        } catch AndroidPendingTurnStoreTestDouble.Failure.save {
            // A failed save must release the owner queue for explicit recovery.
        }
        try await store.discardOwner(ownerID: "owner-a")
        XCTAssertEqual(backing.discardCalls, ["owner-a"])
    }

    private func pending(_ suffix: String) -> AndroidPendingChatTurn {
        AndroidPendingChatTurn(
            conversationID: suffix, userMessageID: "user-\(suffix)", assistantMessageID: "assistant-\(suffix)",
            userText: "Question \(suffix)", assistantText: "Answer \(suffix)",
            modelProvider: "android_aicore", modelName: nil
        )
    }
}
