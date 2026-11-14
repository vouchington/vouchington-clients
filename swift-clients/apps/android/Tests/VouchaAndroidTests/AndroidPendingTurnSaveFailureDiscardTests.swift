import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingSaveDiscardTests: XCTestCase {
    func testFailedSaveBlocksSignOutUntilExplicitOwnerDiscard() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.failSave = true
        let runtime = FakeAICoreRuntime(state: .available, response: "Saved answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime, pendingStore: store)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        viewModel.draft = "Saved question"
        await viewModel.sendOnDevice()

        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
        let path = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        XCTAssertFalse(viewModel.pendingStoreLoadFailed)
        let blockedExit = await viewModel.prepareForAccountExit()
        XCTAssertFalse(blockedExit)
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-a")
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)

        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        XCTAssertEqual(confirmation.ownerID, ownerA)
        XCTAssertTrue(store.discardCalls.isEmpty)
        let discarded = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertTrue(discarded)

        XCTAssertEqual(store.discardCalls, [ownerA])
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertTrue(viewModel.pendingTurns.isEmpty)
        XCTAssertNil(viewModel.chatErrorMessageKey)
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        let allowedExit = await viewModel.prepareForAccountExit()
        XCTAssertTrue(allowedExit)
        viewModel.cancelAccountExit()
    }

    func testConfirmationRequiresIdleStateAtCaptureAndExecution() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.holdSave = true
        let runtime = FakeAICoreRuntime(state: .available, response: "Saved answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime, pendingStore: store)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        viewModel.draft = "Saved question"
        let send = Task { await viewModel.sendOnDevice() }
        await store.waitForSaveEntry()
        XCTAssertNil(viewModel.pendingTurnDiscardConfirmation())
        let path = "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
        fixture.recorder.enqueue(path: path, statusCode: 503, data: Data("{}".utf8))
        store.releaseSave()
        await send.value

        let confirmation = try XCTUnwrap(viewModel.pendingTurnDiscardConfirmation())
        viewModel.isGenerating = true
        let rejected = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertFalse(rejected)
        XCTAssertTrue(store.discardCalls.isEmpty)
        viewModel.isGenerating = false
        let discarded = await viewModel.discardPendingTurns(after: confirmation)
        XCTAssertTrue(discarded)
        XCTAssertEqual(store.discardCalls, [ownerA])
    }
}
