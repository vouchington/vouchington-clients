import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

struct PendingTurnConversationSwitchSetup {
    let fixture: AndroidTranscriptTestClient
    let viewModel: ViewModel
    let firstID: String
    let secondID: String
    let sendPath: String
}

struct PendingHistorySetup {
    let fixture: AndroidTranscriptTestClient
    let viewModel: ViewModel
    let conversationID: String
    let historyPath: String
    let pending: AndroidPendingChatTurn
}

@MainActor
func makeConversationSwitchSetup() async throws -> PendingTurnConversationSwitchSetup {
    let fixture = try AndroidTranscriptTestFactory.makeClient()
    let firstID = "0198ffff-0000-7000-8000-000000000001"
    let secondID = "0198ffff-0000-7000-8000-000000000002"
    let firstHistory = "/api/v1/my/conversations/\(firstID)/messages"
    let sendPath = "/api/v1/conversations/\(firstID)/client-generated-chat"
    fixture.recorder.enqueue(
        path: "/api/v1/my/conversations",
        data: AndroidTranscriptTestFactory.twoConversationListResponse(firstID: firstID, secondID: secondID)
    )
    fixture.recorder.enqueue(path: firstHistory, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
    fixture.recorder.enqueue(
        path: "/api/v1/my/conversations/\(secondID)/messages",
        data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
    )
    fixture.recorder.enqueue(path: firstHistory, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
    fixture.recorder.enqueue(
        path: sendPath,
        statusCode: 503,
        data: Data(#"{"message":"offline"}"#.utf8),
        held: true
    )
    fixture.recorder.enqueue(
        path: sendPath,
        responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
            conversationID: firstID,
            userText: "Hello",
            assistantText: "Hi"
        )
    )
    let viewModel = AndroidTranscriptTestFactory.makeViewModel(
        runtime: FakeAICoreRuntime(state: .available, response: "Hi")
    )
    viewModel.modelState = .available
    await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
    viewModel.draft = "Hello"
    return .init(fixture: fixture, viewModel: viewModel, firstID: firstID, secondID: secondID, sendPath: sendPath)
}

@MainActor
func makePendingHistorySetup() async throws -> PendingHistorySetup {
    let fixture = try AndroidTranscriptTestFactory.makeClient()
    let conversationID = "0198ffff-0000-7000-8000-000000000001"
    let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
    let sendPath = "/api/v1/conversations/\(conversationID)/client-generated-chat"
    fixture.recorder.enqueue(
        path: "/api/v1/my/conversations",
        data: AndroidTranscriptTestFactory.conversationListResponse(id: conversationID, title: "Chat")
    )
    fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
    fixture.recorder.enqueue(path: sendPath, statusCode: 503, data: Data(#"{"message":"offline"}"#.utf8))
    let viewModel = AndroidTranscriptTestFactory.makeViewModel(
        runtime: FakeAICoreRuntime(state: .available, response: "Local answer")
    )
    viewModel.modelState = .available
    await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
    viewModel.draft = "Question"
    await viewModel.sendOnDevice()
    let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
    return .init(
        fixture: fixture,
        viewModel: viewModel,
        conversationID: conversationID,
        historyPath: historyPath,
        pending: pending
    )
}

func historyPage(_ messages: [[String: Any]], hasNextPage: Bool, cursor: String?) throws -> Data {
    let endCursor: Any = cursor ?? NSNull()
    let response: [String: Any] = [
        "results": messages,
        "page_info": ["has_next_page": hasNextPage, "end_cursor": endCursor]
    ]
    return try JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])
}

func canonicalJSONBody(_ body: Data?) throws -> Data {
    let object = try XCTUnwrap(body).jsonObject()
    return try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])
}

func serverMessage(id: String, conversationID: String, role: String, text: String) -> [String: Any] {
    [
        "id": id,
        "conversation_id": conversationID,
        "created_at": "2026-10-17T12:00:00.000Z",
        "updated_at": "2026-10-17T12:00:00.000Z",
        "content": ["role": role, "content": text],
        "completion": ["status": "completed"]
    ]
}
