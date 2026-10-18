import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidConversationSwitchTests: XCTestCase {
    func testSendDuringHistoryLoadDoesNotGenerateOrReplaceLoadedHistory() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let messagesPath = "/api/v1/my/conversations/\(conversationID)/messages"
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try fixture.recorder.enqueue(
            path: messagesPath,
            data: AndroidTranscriptFixtures.data("native.chat.page-1"),
            held: true
        )
        let runtime = FakeAICoreRuntime(state: .available, response: "Unexpected generation")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        let binding = Task { await viewModel.bindAccount(client: fixture.client, userID: "fixture-user") }
        _ = try await fixture.recorder.waitForRequest(path: messagesPath, occurrence: 1)

        viewModel.draft = "Question while loading"
        await viewModel.sendOnDevice()

        XCTAssertTrue(viewModel.isLoadingMessages)
        XCTAssertTrue(viewModel.messages.isEmpty)
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 0)
        try fixture.recorder.completeHeld(
            path: messagesPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )
        await binding.value

        XCTAssertEqual(viewModel.messages.map(\.id), ["0198ffff-0001-7000-8000-000000000002"])
        XCTAssertEqual(viewModel.messages.first?.content, "Hi")
    }

    func testLateGeneratedTurnPersistsToOriginWithoutReplacingSelectedTranscript() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let postPath = "/api/v1/conversations/\(firstID)/client-generated-chat"
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: fixture.recorder,
            firstID: firstID,
            secondID: secondID
        )
        AndroidTranscriptTestFactory.enqueueHeldGeneratedTurn(
            on: fixture.recorder,
            conversationID: firstID,
            userText: "Question for first",
            assistantText: "Answer for first"
        )

        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Question for first"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        await viewModel.selectConversation(secondID)
        await runtime.finish("Answer for first")
        let post = try await fixture.recorder.waitForRequest(path: postPath, occurrence: 1)
        let body = try XCTUnwrap(post.httpBody).jsonObject()
        try fixture.recorder.completeHeld(
            path: postPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.generatedChatResponse(
                conversationID: firstID,
                userText: "Question for first",
                assistantText: "Answer for first",
                userMessageID: XCTUnwrap(body["user_message_id"] as? String),
                assistantMessageID: XCTUnwrap(body["assistant_message_id"] as? String)
            )
        )
        await send.value

        XCTAssertEqual(viewModel.selectedConversationID, secondID)
        XCTAssertEqual(viewModel.messages.map(\.id), ["second-conversation-message"])
        XCTAssertNil(viewModel.pendingTurns[firstID])
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: postPath).count, 1)
    }

    func testLateHistoryReadCannotReplaceFreshReadAfterConversationSwitch() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let firstPath = "/api/v1/my/conversations/\(firstID)/messages"
        AndroidTranscriptTestFactory.enqueueConversationHistoryRace(
            on: fixture.recorder,
            firstID: firstID,
            secondID: secondID
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        let binding = Task { await viewModel.bindAccount(client: fixture.client, userID: "fixture-user") }
        _ = try await fixture.recorder.waitForRequest(path: firstPath, occurrence: 1)

        await viewModel.selectConversation(secondID)
        await viewModel.selectConversation(firstID)
        fixture.recorder.completeHeld(
            path: firstPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.messagePageResponse(
                id: "stale-first-read",
                conversationID: firstID,
                content: "Stale first read"
            )
        )
        await binding.value

        XCTAssertEqual(viewModel.selectedConversationID, firstID)
        XCTAssertEqual(viewModel.messages.map(\.id), ["fresh-first-read"])
        XCTAssertEqual(viewModel.messages.first?.content, "Fresh first read")
    }
}
