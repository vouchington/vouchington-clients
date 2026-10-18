import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest
#if !canImport(Darwin)
    import FoundationNetworking
#endif

@MainActor
final class AndroidTranscriptMutationRaceTests: XCTestCase {
    func testResetListRetryWaitsForConversationCreation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let listPath = "/api/v1/my/conversations"
        let createPath = "/api/v1/conversations"
        let createdID = AndroidTranscriptTestFactory.newConversationID
        fixture.recorder.enqueue(path: listPath, statusCode: 503, data: Data())
        fixture.recorder.enqueue(
            path: listPath,
            data: Data(#"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8)
        )
        fixture.recorder.enqueue(
            path: createPath,
            data: AndroidTranscriptTestFactory.conversationResponse(id: createdID, title: "Created"),
            held: true
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(createdID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let creation = Task { await viewModel.createConversation() }
        _ = try await fixture.recorder.waitForRequest(path: createPath, occurrence: 1)

        await viewModel.retryConversationLoad()

        XCTAssertEqual(fixture.recorder.requestSnapshot(path: listPath).count, 1)
        XCTAssertNotNil(viewModel.failedConversationListLoad)
        fixture.recorder.completeHeld(
            path: createPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.conversationResponse(id: createdID, title: "Created")
        )
        let created = await creation.value
        XCTAssertTrue(created)
        XCTAssertEqual(viewModel.conversations.map(\.id), [createdID])
        XCTAssertEqual(viewModel.selectedConversationID, createdID)
    }

    func testOlderPageWaitsForGenerationFailureAndRemainsLoadable() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"))
        try fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-2"))
        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "A failing generation"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()

        await viewModel.loadOlderMessages()
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: historyPath).count, 1)
        XCTAssertTrue(viewModel.hasOlderMessages)
        await runtime.fail("generation unavailable")
        await send.value
        await viewModel.loadOlderMessages()

        XCTAssertEqual(viewModel.messages.map(\.id), [
            "0198ffff-0001-7000-8000-000000000001",
            "0198ffff-0001-7000-8000-000000000002"
        ])
        XCTAssertFalse(viewModel.hasOlderMessages)
        XCTAssertEqual(viewModel.draft, "A failing generation")
    }

    func testResetHistoryPreservesTurnSavedAfterRequestStarted() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let firstHistory = "/api/v1/my/conversations/\(firstID)/messages"
        let savePath = "/api/v1/conversations/\(firstID)/client-generated-chat"
        enqueueResetHistoryRace(on: fixture.recorder, firstID: firstID, secondID: secondID)
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let pending = AndroidPendingChatTurn(
            conversationID: firstID,
            userMessageID: AndroidTranscriptTestFactory.generatedUserMessageID,
            assistantMessageID: AndroidTranscriptTestFactory.generatedAssistantMessageID,
            userText: "Saved question",
            assistantText: "Saved answer",
            modelProvider: "android_aicore",
            modelName: "android-aicore-system"
        )
        viewModel.pendingTurns[firstID] = pending
        let retry = Task { await viewModel.retryPendingTurn() }
        _ = try await fixture.recorder.waitForRequest(path: savePath, occurrence: 1)
        await viewModel.selectConversation(secondID)
        let selection = Task { await viewModel.selectConversation(firstID) }
        _ = try await fixture.recorder.waitForRequest(path: firstHistory, occurrence: 2)

        fixture.recorder.completeHeld(
            path: savePath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.generatedChatResponse(
                conversationID: firstID,
                userText: pending.userText,
                assistantText: pending.assistantText
            )
        )
        await retry.value
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
        fixture.recorder.completeHeld(
            path: firstHistory,
            occurrence: 2,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await selection.value

        XCTAssertEqual(viewModel.messages.map(\.id), [pending.userMessageID, pending.assistantMessageID])
        XCTAssertEqual(viewModel.messages.map(\.content), [pending.userText, pending.assistantText])
        XCTAssertEqual(viewModel.loadedConversationHistoryID, firstID)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: savePath).count, 1)
    }

    private func enqueueResetHistoryRace(on recorder: AndroidTranscriptRecorder, firstID: String, secondID: String) {
        let firstHistory = "/api/v1/my/conversations/\(firstID)/messages"
        let secondHistory = "/api/v1/my/conversations/\(secondID)/messages"
        let savePath = "/api/v1/conversations/\(firstID)/client-generated-chat"
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.twoConversationListResponse(firstID: firstID, secondID: secondID)
        )
        recorder.enqueue(path: firstHistory, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
        recorder.enqueue(
            path: firstHistory,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse(),
            held: true
        )
        recorder.enqueue(path: secondHistory, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
        recorder.enqueue(
            path: savePath,
            held: true,
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: firstID,
                userText: "Saved question",
                assistantText: "Saved answer"
            )
        )
    }
}
