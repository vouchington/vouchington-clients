import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidTranscriptResponseValidationTests: XCTestCase {
    func testMismatchedSuccessfulResponseKeepsPendingTurnForExactRetry() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let postPath = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        try enqueueResponses(on: fixture.recorder, path: postPath, conversationID: conversationID)

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Answer")
        )
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Question"
        await viewModel.sendOnDevice()

        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
        assertRetainedTurn(viewModel, recorder: fixture.recorder, path: postPath, pending: pending, count: 1)
        await retryInvalidResponses(viewModel, recorder: fixture.recorder, path: postPath, pending: pending)
        await viewModel.retryPendingTurn()

        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        let request = try XCTUnwrap(fixture.recorder.requestSnapshot(path: postPath).last)
        let body = try XCTUnwrap(request.httpBody).jsonObject()
        XCTAssertEqual(viewModel.messages.suffix(2).map(\.id), try [
            XCTUnwrap(body["user_message_id"] as? String),
            XCTUnwrap(body["assistant_message_id"] as? String)
        ])
    }
}

@MainActor
private extension AndroidTranscriptResponseValidationTests {
    func enqueueResponses(on recorder: AndroidTranscriptRecorder, path: String, conversationID: String) throws {
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: recorder)
        recorder.enqueue(
            path: path,
            responseBuilder: AndroidTranscriptTestFactory.mismatchedTurnResponseBuilder(
                conversationID: conversationID,
                userText: "Question",
                assistantText: "Answer"
            )
        )
        recorder.enqueue(
            path: path,
            responseBuilder: AndroidTranscriptTestFactory.mismatchedMessageIDResponseBuilder(
                conversationID: conversationID,
                userText: "Question",
                assistantText: "Answer"
            )
        )
        recorder.enqueue(
            path: path,
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: "different-conversation",
                userText: "Question",
                assistantText: "Answer"
            )
        )
        recorder.enqueue(
            path: path,
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: conversationID,
                userText: "Question",
                assistantText: "Answer"
            )
        )
    }

    func retryInvalidResponses(
        _ viewModel: ViewModel,
        recorder: AndroidTranscriptRecorder,
        path: String,
        pending: AndroidPendingChatTurn
    ) async {
        for requestCount in 2 ... 3 {
            await viewModel.retryPendingTurn()
            assertRetainedTurn(viewModel, recorder: recorder, path: path, pending: pending, count: requestCount)
        }
    }

    func assertRetainedTurn(
        _ viewModel: ViewModel,
        recorder: AndroidTranscriptRecorder,
        path: String,
        pending: AndroidPendingChatTurn,
        count: Int
    ) {
        XCTAssertEqual(viewModel.pendingTurnForSelectedConversation, pending)
        XCTAssertTrue(viewModel.messages.contains { $0.id == pending.userMessageID })
        XCTAssertTrue(viewModel.messages.contains { $0.id == pending.assistantMessageID })
        XCTAssertEqual(recorder.requestSnapshot(path: path).count, count)
    }
}
