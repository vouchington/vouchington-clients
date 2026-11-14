import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class PendingTurnDiscardRaceTests: XCTestCase {
    func testOldHeldPostCannotRestoreDiscardedTurnOrChangeNewOwner() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let model = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Saved answer"), pendingStore: store
        )
        model.modelState = .available
        await model.bindAccount(client: fixture.client, userID: "owner-a")
        let conversationID = try XCTUnwrap(model.selectedConversationID)
        let postPath = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        fixture.recorder.enqueue(path: postPath, data: Data(), held: true)
        model.draft = "Saved question"
        let send = Task { await model.sendOnDevice() }
        let heldRequest = try await fixture.recorder.waitForRequest(path: postPath, occurrence: 1)
        let pendingA = try XCTUnwrap(model.pendingTurnForSelectedConversation)
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        let ownerB = AndroidTranscriptTestFactory.pendingOwnerKey("owner-b", client: fixture.client)

        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "owner-a")
        XCTAssertEqual(model.pendingTurnForSelectedConversation, pendingA)
        let confirmation = try XCTUnwrap(model.pendingTurnDiscardConfirmation())
        let discarded = await model.discardPendingTurns(after: confirmation)
        XCTAssertTrue(discarded)
        XCTAssertNil(store.records[ownerA])

        let pendingB = AndroidPendingChatTurn(
            conversationID: "other-conversation", userMessageID: "other-user", assistantMessageID: "other-assistant",
            userText: "Other question", assistantText: "Other answer",
            modelProvider: "android_aicore", modelName: nil
        )
        store.records[ownerB] = [pendingB.conversationID: pendingB]
        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "owner-b")
        XCTAssertEqual(model.unlistedPendingTurns, [pendingB])

        let response = AndroidTranscriptTestFactory.generatedChatResponseBuilder(
            conversationID: conversationID, userText: pendingA.userText, assistantText: pendingA.assistantText
        )(heldRequest)
        fixture.recorder.completeHeld(path: postPath, occurrence: 1, statusCode: 200, data: response)
        await send.value
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: postPath).count, 1)
        XCTAssertEqual(store.records[ownerB]?[pendingB.conversationID], pendingB)
        XCTAssertNil(store.records[ownerA])

        await model.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        await model.bindAccount(client: fixture.client, userID: "owner-a")
        XCTAssertNil(model.pendingTurnForSelectedConversation)
        XCTAssertTrue(model.unlistedPendingTurns.isEmpty)
    }
}
