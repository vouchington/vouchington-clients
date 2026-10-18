import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidPendingTurnAccountBoundaryTests: XCTestCase {
    func testFailedSaveBlocksSignOutUntilEncryptedTurnCanBeAcknowledged() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.failSave = true
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Saved answer"),
            pendingStore: store
        )
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        viewModel.draft = "Saved question"
        await viewModel.sendOnDevice()
        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)

        let replacement = AndroidAccountReplacementCoordinator()
        var logoutCalls = 0
        await replacement.perform(operation: {
            guard await viewModel.prepareForAccountExit() else { return false }
            logoutCalls += 1
            await viewModel.bindAccount(client: nil, userID: nil)
            return true
        }, isCurrent: { true }, replace: {})

        XCTAssertEqual(logoutCalls, 0)
        XCTAssertEqual(viewModel.currentAccountUserID, "owner-a")
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertNotNil(viewModel.accountExitErrorKey)
        XCTAssertTrue(store.records[ownerA, default: [:]].isEmpty)

        store.failSave = false
        await replacement.perform(operation: {
            guard await viewModel.prepareForAccountExit() else { return false }
            logoutCalls += 1
            await viewModel.bindAccount(client: nil, userID: nil)
            return true
        }, isCurrent: { true }, replace: {})

        XCTAssertEqual(logoutCalls, 1)
        XCTAssertNil(viewModel.currentAccountUserID)
        XCTAssertEqual(store.records[ownerA]?[pending.conversationID], pending)
    }

    func testHeldInitialLoadBlocksGenerationAndConversationCreation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.holdNextLoad = true
        let runtime = FakeAICoreRuntime(state: .available, response: "Answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime, pendingStore: store)
        viewModel.modelState = .available

        let binding = Task { await viewModel.bindAccount(client: fixture.client, userID: "owner-a") }
        await store.waitForLoadEntry()
        viewModel.draft = "Question"
        await viewModel.sendOnDevice()
        let created = await viewModel.createConversation()
        XCTAssertFalse(created)
        XCTAssertTrue(viewModel.pendingStoreIsLoading)
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 0)
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: "/api/v1/conversations").isEmpty)
        store.releaseLoad()
        await binding.value
        XCTAssertFalse(viewModel.pendingStoreIsLoading)
    }

    func testHeldLoadCannotOverwriteActivePendingOrStartRestoreRetry() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        store.holdNextLoad = true
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        let binding = Task { await viewModel.bindAccount(client: fixture.client, userID: "owner-a") }
        await store.waitForLoadEntry()
        let pending = AndroidPendingChatTurn(
            conversationID: AndroidTranscriptTestFactory.newConversationID,
            userMessageID: "pending-user", assistantMessageID: "pending-assistant",
            userText: "Question", assistantText: "Answer", modelProvider: "android_aicore", modelName: nil
        )
        viewModel.pendingTurns[pending.conversationID] = pending
        viewModel.selectedConversationID = pending.conversationID
        await viewModel.retryPendingTurn()
        let path = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: path).isEmpty)
        store.releaseLoad()
        await binding.value
        XCTAssertEqual(viewModel.pendingTurns[pending.conversationID], pending)
    }

    func testStaleSameOwnerLoadCannotReplaceNewerBinding() async throws {
        let firstFixture = try AndroidTranscriptTestFactory.makeClient()
        let firstOwner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: firstFixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: firstFixture.host) }
        let secondFixture = try AndroidTranscriptTestFactory.makeClient()
        let secondOwner = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: secondFixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: secondFixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: secondFixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let old = AndroidPendingChatTurn(
            conversationID: "old-conversation", userMessageID: "old-user", assistantMessageID: "old-assistant",
            userText: "Old", assistantText: "Old answer", modelProvider: "android_aicore", modelName: nil
        )
        let newer = AndroidPendingChatTurn(
            conversationID: "new-conversation", userMessageID: "new-user", assistantMessageID: "new-assistant",
            userText: "New", assistantText: "New answer", modelProvider: "android_aicore", modelName: nil
        )
        store.records[firstOwner] = [old.conversationID: old]
        store.holdNextLoad = true
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        let staleBinding = Task { await viewModel.bindAccount(client: firstFixture.client, userID: "owner-a") }
        await store.waitForLoadEntry()
        store.records[secondOwner] = [newer.conversationID: newer]
        await viewModel.bindAccount(client: secondFixture.client, userID: "owner-a")
        store.releaseLoad()
        await staleBinding.value

        XCTAssertNil(viewModel.pendingTurns[old.conversationID])
        XCTAssertEqual(viewModel.pendingTurns[newer.conversationID], newer)
        XCTAssertEqual(store.records[firstOwner]?[old.conversationID], old)
        XCTAssertTrue(firstFixture.recorder.requestSnapshot(path: "/api/v1/my/conversations").isEmpty)
    }

    func testRestoredTurnOutsideFirstPageIsSelectableForExplicitRetry() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let ownerA = AndroidTranscriptTestFactory.pendingOwnerKey("owner-a", client: fixture.client)
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let store = AndroidPendingTurnStoreTestDouble()
        let pending = AndroidPendingChatTurn(
            conversationID: "unlisted-conversation", userMessageID: "unlisted-user",
            assistantMessageID: "unlisted-assistant", userText: "Unlisted question",
            assistantText: "Unlisted answer", modelProvider: "android_aicore", modelName: nil
        )
        store.records[ownerA] = [pending.conversationID: pending]
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available), pendingStore: store
        )
        await viewModel.bindAccount(client: fixture.client, userID: "owner-a")
        XCTAssertEqual(viewModel.unlistedPendingTurns, [pending])
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(pending.conversationID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )

        await viewModel.selectConversation(pending.conversationID)

        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        let postPath = "/api/v1/conversations/\(pending.conversationID)/client-generated-chat"
        XCTAssertTrue(fixture.recorder.requestSnapshot(path: postPath).isEmpty)
    }
}
