import Foundation
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidAutoSendDraftRetentionTests: XCTestCase {
    func testAutoSendPreservesNewerDraftTypedDuringCreatedHistoryLoad() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        fixture.recorder.enqueue(path: "/api/v1/my/conversations", data: Data(
            #"{"results":[],"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8
        ))
        fixture.recorder.enqueue(
            path: "/api/v1/conversations",
            data: AndroidTranscriptTestFactory.conversationResponse(id: conversationID, title: "Created")
        )
        fixture.recorder.enqueue(
            path: historyPath,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse(),
            held: true
        )
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            conversationID: conversationID,
            userText: "Original question",
            assistantText: "Generated answer"
        )
        let runtime = FakeAICoreRuntime(state: .available, response: "Generated answer")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original question"

        let send = Task { await viewModel.sendOnDevice() }
        _ = try await fixture.recorder.waitForRequest(path: historyPath, occurrence: 1)
        viewModel.draft = "Next question"
        fixture.recorder.completeHeld(
            path: historyPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await send.value

        XCTAssertEqual(viewModel.draft, "Next question")
        let request = try XCTUnwrap(fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat"
        ).first)
        let payload = try XCTUnwrap(request.httpBody).jsonObject()
        XCTAssertEqual(payload["message"] as? String, "Original question")
        XCTAssertEqual(viewModel.messages.map(\.content), ["Original question", "Generated answer"])
    }
}
