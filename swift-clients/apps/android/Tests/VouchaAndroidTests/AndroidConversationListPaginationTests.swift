import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidConversationListPaginationTests: XCTestCase {
    func testConversationListPaginationForwardsCursorAndDeduplicatesConversations() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let host = fixture.host
        let recorder = fixture.recorder
        let client = fixture.client
        defer { AndroidTranscriptURLProtocol.uninstall(host: host) }
        let firstID = "0198ffff-0000-7000-8000-000000000001"
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: firstID,
                title: "First",
                hasNextPage: true,
                endCursor: "first-cursor"
            )
        )
        try recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.duplicateConversationListPage(
                firstID: firstID,
                secondID: secondID
            )
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: client, userID: "fixture-user")
        await viewModel.loadMoreConversations()

        XCTAssertEqual(viewModel.conversations.map(\.id), [firstID, secondID])
        let requests = recorder.requestSnapshot(path: "/api/v1/my/conversations")
        let query = try XCTUnwrap(URLComponents(url: XCTUnwrap(requests.last?.url), resolvingAgainstBaseURL: false)?
            .queryItems)
        XCTAssertTrue(query.contains(URLQueryItem(name: "after", value: "first-cursor")))
    }

    func testFailedInitialConversationLoadCanRetryWithoutRebindingAccount() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let listPath = "/api/v1/my/conversations"
        fixture.recorder.enqueue(
            path: listPath,
            statusCode: 503,
            data: Data(#"{"message":"offline"}"#.utf8)
        )
        try fixture.recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/0198ffff-0000-7000-8000-000000000001/messages",
            data: AndroidTranscriptFixtures.data("native.chat.page-1")
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        XCTAssertTrue(viewModel.conversations.isEmpty)
        XCTAssertNotNil(viewModel.errorMessage)

        await viewModel.retryConversationLoad()

        XCTAssertEqual(fixture.recorder.requestSnapshot(path: listPath).count, 2)
        XCTAssertEqual(viewModel.selectedConversationID, "0198ffff-0000-7000-8000-000000000001")
        XCTAssertEqual(viewModel.messages.first?.content, "Hi")
        XCTAssertNil(viewModel.errorMessage)
    }

    func testFailedConversationHistoryCanRetrySelectedConversation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = "0198ffff-0000-7000-8000-000000000001"
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        try fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptFixtures.data("native.chat.conversations")
        )
        fixture.recorder.enqueue(path: historyPath, statusCode: 503, data: Data(#"{"message":"offline"}"#.utf8))
        try fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"))

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        XCTAssertEqual(viewModel.selectedConversationID, conversationID)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertNotNil(viewModel.errorMessage)

        await viewModel.retryConversationLoad()

        XCTAssertEqual(fixture.recorder.requestSnapshot(path: historyPath).count, 2)
        XCTAssertEqual(viewModel.messages.first?.content, "Hi")
        XCTAssertNil(viewModel.errorMessage)
    }

}
