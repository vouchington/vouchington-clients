import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnStorageTests: XCTestCase {
    func testFailedStoreSaveBlocksFirstPOSTAndRetainsCompletedTurn() async throws {
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

        let path = "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        XCTAssertNotNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertTrue(store.records[ownerA, default: [:]].isEmpty)
        XCTAssertFalse(viewModel.canSendTranscriptMessage)
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 1)
    }

    func testFailedDurableDeleteRetainsExactBytesForRetry() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.failDelete = true
        let runtime = FakeAICoreRuntime(state: .available, response: "Saved answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime, pendingStore: store)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder, userText: "Saved question", assistantText: "Saved answer"
        )
        viewModel.draft = "Saved question"

        await viewModel.sendOnDevice()

        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(store.records[ownerA]?[pending.conversationID], pending)
        store.failDelete = false
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder, userText: "Saved question", assistantText: "Saved answer"
        )
        await viewModel.retryPendingTurn()

        let path = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        let posts = fixture.recorder.requestSnapshot(path: path)
        XCTAssertEqual(posts.count, 2)
        XCTAssertEqual(posts[0].httpBody, pending.requestBody)
        XCTAssertEqual(posts[1].httpBody, pending.requestBody)
        XCTAssertEqual(store.savedBodies, [pending.requestBody, pending.requestBody])
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertNil(store.records[ownerA]?[pending.conversationID])
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 1)
    }

    func testSignOutRetainsOnlySameOwnersTurnWithoutAutomaticPOST() async throws {
        let firstFixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: firstFixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: firstFixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: firstFixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let runtime = FakeAICoreRuntime(state: .available, response: "Saved answer")
        let first = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime, pendingStore: store)
        first.modelState = .available
        await first.bindAccount(client: firstFixture.client, userID: "owner-a")
        let path = "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
        firstFixture.recorder.enqueue(path: path, statusCode: 503, data: Data("{}".utf8))
        first.draft = "Saved question"
        await first.sendOnDevice()
        let saved = try XCTUnwrap(first.pendingTurnForSelectedConversation)
        await first.bindAccount(client: nil, userID: nil)
        XCTAssertNil(first.pendingTurnForSelectedConversation)
        XCTAssertEqual(store.records[ownerA]?[saved.conversationID], saved)

        let resumedFixture = try AndroidTranscriptTestFactory.makeClient(host: firstFixture.host)
        defer { AndroidTranscriptURLProtocol.uninstall(host: resumedFixture.host) }
        let resumedRuntime = FakeAICoreRuntime(state: .available, response: "Should not generate")
        let resumed = AndroidTranscriptTestFactory.makeViewModel(runtime: resumedRuntime, pendingStore: store)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: resumedFixture.recorder)
        await resumed.bindAccount(client: resumedFixture.client, userID: "owner-b")
        XCTAssertNil(resumed.pendingTurnForSelectedConversation)
        XCTAssertTrue(resumedFixture.recorder.requestSnapshot(path: path).isEmpty)

        await resumed.bindAccount(client: nil, userID: nil)
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: resumedFixture.recorder)
        await resumed.bindAccount(client: resumedFixture.client, userID: "owner-a")
        XCTAssertEqual(resumed.pendingTurnForSelectedConversation, saved)
        XCTAssertTrue(resumedFixture.recorder.requestSnapshot(path: path).isEmpty)
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: resumedFixture.recorder, userText: "Saved question", assistantText: "Saved answer"
        )
        await resumed.retryPendingTurn()
        XCTAssertEqual(resumedFixture.recorder.requestSnapshot(path: path).first?.httpBody, saved.requestBody)
        let resumedGeneration = await resumedRuntime.generationSnapshot()
        XCTAssertEqual(resumedGeneration.callCount, 0)
        XCTAssertNil(store.records[ownerA]?[saved.conversationID])
    }

    func testAccountChangeWhileSaveIsPendingPreventsOldOwnersPOST() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.holdSave = true
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Saved answer"),
            pendingStore: store
        )
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        viewModel.draft = "Saved question"

        let send = Task { await viewModel.sendOnDevice() }
        await store.waitForSaveEntry()
        store.holdSave = false
        let invalidation = Task { await viewModel.bindAccount(client: nil, userID: nil) }
        await Task.yield()
        XCTAssertNil(viewModel.currentAccountUserID)
        store.releaseSave()
        await invalidation.value
        await send.value

        let path = "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        XCTAssertNil(viewModel.currentAccountUserID)
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(store.records[ownerA]?.count, 1)
    }

}
