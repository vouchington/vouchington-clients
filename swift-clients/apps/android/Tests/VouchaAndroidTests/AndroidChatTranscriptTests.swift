import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
@testable import VouchaAPI
import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class AndroidChatTranscriptTests: XCTestCase {
    func testCompletedTurnRetriesSameIdsAndPayloadThroughAuthenticatedClient() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let host = fixture.host
        let recorder = fixture.recorder
        let client = fixture.client
        defer { AndroidTranscriptURLProtocol.uninstall(host: host) }
        let conversationID = "0198ffff-0000-7000-8000-000000000001"
        try recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        try recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"))
        try recorder.enqueue(
            path: historyPath,
            data: AndroidTranscriptFixtures.data("native.chat.page-1"),
            held: true
        )
        recorder.enqueue(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat",
            statusCode: 503,
            data: Data(#"{"message":"retry"}"#.utf8)
        )
        recorder.enqueue(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat",
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: conversationID,
                userText: "Hello",
                assistantText: "Hi"
            )
        )

        let runtime = FakeAICoreRuntime(state: .available, response: "Hi")
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: client, userID: "fixture-user")
        viewModel.draft = "Hello"
        await viewModel.sendOnDevice()

        XCTAssertNotNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.messages.last?.content, "Hi")
        try await assertHistoryReloadBlocksRetry(
            viewModel,
            recorder: recorder,
            historyPath: historyPath,
            conversationID: conversationID
        )
        await viewModel.retryPendingTurn()
        try await assertCompletedRetry(viewModel, recorder: recorder, conversationID: conversationID)
    }

    func testLateHistoryResponseCannotReplaceNewAccountTranscript() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let host = fixture.host
        let recorder = fixture.recorder
        let client = fixture.client
        defer { AndroidTranscriptURLProtocol.uninstall(host: host) }
        let oldID = "0198ffff-0000-7000-8000-000000000001"
        let oldPath = "/api/v1/my/conversations/\(oldID)/messages"
        let newID = "conversation-new-account"
        try recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try recorder.enqueue(path: oldPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"), held: true)
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(id: newID, title: "New account")
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(newID)/messages",
            data: AndroidTranscriptTestFactory.messagePageResponse(
                id: "new-account-message",
                conversationID: newID,
                content: "New account history"
            )
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        let oldBinding = Task { await viewModel.bindAccount(client: client, userID: "old-user") }
        _ = try await recorder.waitForRequest(path: oldPath, occurrence: 1)
        await viewModel.bindAccount(client: client, userID: "new-user")
        try recorder.completeHeld(
            path: oldPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )
        await oldBinding.value

        XCTAssertEqual(viewModel.currentAccountUserID, "new-user")
        XCTAssertEqual(viewModel.selectedConversationID, newID)
        XCTAssertEqual(viewModel.messages.map(\.id), ["new-account-message"])
        XCTAssertEqual(viewModel.messages.first?.content, "New account history")
    }

}

@MainActor
private extension AndroidChatTranscriptTests {
    func assertHistoryReloadBlocksRetry(
        _ viewModel: ViewModel,
        recorder: AndroidTranscriptRecorder,
        historyPath: String,
        conversationID: String
    ) async throws {
        let reload = Task { await viewModel.loadConversationMessages(reset: true) }
        _ = try await recorder.waitForRequest(path: historyPath, occurrence: 2)
        await viewModel.retryPendingTurn()
        XCTAssertEqual(
            recorder.requestSnapshot(path: "/api/v1/conversations/\(conversationID)/client-generated-chat").count,
            1
        )
        XCTAssertNotNil(viewModel.pendingTurnForSelectedConversation)
        try recorder.completeHeld(
            path: historyPath,
            occurrence: 2,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await reload.value
    }

    func assertCompletedRetry(
        _ viewModel: ViewModel,
        recorder: AndroidTranscriptRecorder,
        conversationID: String
    ) async throws {
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.messages.map(\.role), ["user", "assistant"])
        let path = "/api/v1/conversations/\(conversationID)/client-generated-chat"
        let posts = recorder.requestSnapshot(path: path)
        XCTAssertEqual(posts.count, 2)
        let firstBody = try XCTUnwrap(posts[0].httpBody).jsonObject()
        let retryBody = try XCTUnwrap(posts[1].httpBody).jsonObject()
        XCTAssertEqual(viewModel.messages.map(\.id), try [
            XCTUnwrap(firstBody["user_message_id"] as? String),
            XCTUnwrap(firstBody["assistant_message_id"] as? String)
        ])
        XCTAssertEqual(
            try JSONSerialization.data(withJSONObject: firstBody, options: [.sortedKeys]),
            try JSONSerialization.data(withJSONObject: retryBody, options: [.sortedKeys])
        )
        XCTAssertEqual(firstBody["model_provider"] as? String, "android_aicore")
        XCTAssertEqual(firstBody["model_name"] as? String, "android-aicore-system")
        // Darwin URLProtocol intercepts before Cookie injection; APIClientCookiePersistenceTests
        // verifies delivery over real HTTP, while this checks the bound authenticated session.
        let client = try XCTUnwrap(viewModel.apiClient)
        let jar = await client.cookieStorage
        let session = await client.session
        XCTAssertTrue(session.configuration.httpCookieStorage === jar)
        XCTAssertTrue(session.configuration.httpShouldSetCookies)
        let postURL = try XCTUnwrap(posts[0].url)
        let cookies = try XCTUnwrap(jar.cookies(for: postURL))
        XCTAssertEqual(cookies.map { "\($0.name)=\($0.value)" }, ["st=fixture-session"])
        XCTAssertFalse(recorder.requestSnapshot().contains { $0.url?.path == "/api/v1/chat" })
    }
}
