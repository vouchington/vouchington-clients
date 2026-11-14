import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidPendingAcknowledgementTests: XCTestCase {
    func testLateInitialPOSTFailureCannotResurrectTurnAcknowledgedByHistory() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let path = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        fixture.recorder.enqueue(path: path, statusCode: 503, data: Data("{}".utf8), held: true)
        let runtime = FakeAICoreRuntime(state: .available, response: "Generated answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original question"
        let send = Task { await viewModel.sendOnDevice() }
        _ = try await fixture.recorder.waitForRequest(path: path, occurrence: 1)
        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(conversationID)/messages",
            data: historyPage([
                serverMessage(
                    id: pending.userMessageID,
                    conversationID: conversationID,
                    role: "user",
                    text: "Canonical question"
                ),
                serverMessage(
                    id: pending.assistantMessageID,
                    conversationID: conversationID,
                    role: "assistant",
                    text: "Canonical answer"
                )
            ], hasNextPage: false, cursor: nil)
        )
        await viewModel.loadConversationMessages(reset: true)
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        fixture.recorder.completeHeld(path: path, occurrence: 1, statusCode: 503, data: Data("{}".utf8))
        await send.value
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertNil(viewModel.failedGenerationForSelectedConversation)
        XCTAssertEqual(viewModel.draft, "")
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.messages.map(\.content), ["Canonical question", "Canonical answer"])
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: path).count, 1)
        let generation = await runtime.generationSnapshot()
        XCTAssertEqual(generation.callCount, 1)
    }
}
