import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidConversationRetryTests: XCTestCase {
    func testRetryAfterConversationListPageFailureRepeatsListCursorInsteadOfHistory() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let listPath = "/api/v1/my/conversations"
        let historyPath = "/api/v1/my/conversations/\(firstID)/messages"
        fixture.recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: firstID,
                title: "First",
                hasNextPage: true,
                endCursor: "list-page-cursor"
            )
        )
        try fixture.recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"))
        fixture.recorder.enqueue(
            path: listPath,
            statusCode: 503,
            data: Data(#"{"message":"offline"}"#.utf8)
        )
        fixture.recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: secondID,
                title: "Second"
            )
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        await viewModel.loadMoreConversations()

        XCTAssertEqual(viewModel.selectedConversationID, firstID)
        XCTAssertEqual(viewModel.transcriptLoadFailure, .conversations(reset: false, after: "list-page-cursor"))
        XCTAssertNotNil(viewModel.errorMessage)
        await viewModel.retryConversationLoad()

        let listRequests = fixture.recorder.requestSnapshot(path: listPath)
        XCTAssertEqual(listRequests.count, 3)
        let retryURL = try XCTUnwrap(listRequests.last?.url)
        let retryQuery = try XCTUnwrap(URLComponents(url: retryURL, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertTrue(retryQuery.contains(URLQueryItem(name: "after", value: "list-page-cursor")))
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: historyPath).count, 1)
        XCTAssertEqual(viewModel.conversations.map(\.id), [firstID, secondID])
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.transcriptLoadFailed)
    }

    func testListRetryDoesNotDiscardConcurrentInitialHistoryFailure() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let listPath = "/api/v1/my/conversations"
        let historyPath = "/api/v1/my/conversations/\(firstID)/messages"
        try enqueueConcurrentFailures(on: fixture.recorder, firstID: firstID, secondID: secondID)

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        viewModel.modelState = .available
        let binding = Task { await viewModel.bindAccount(client: fixture.client, userID: "fixture-user") }
        _ = try await fixture.recorder.waitForRequest(path: historyPath, occurrence: 1)
        let listPage = Task { await viewModel.loadMoreConversations() }
        _ = try await fixture.recorder.waitForRequest(path: listPath, occurrence: 2)

        fixture.recorder.completeHeld(path: historyPath, occurrence: 1, statusCode: 503, data: Data())
        await binding.value
        XCTAssertEqual(viewModel.failedConversationHistoryLoad?.operation, .messages(
            conversationID: firstID,
            reset: true,
            after: nil
        ))
        fixture.recorder.completeHeld(path: listPath, occurrence: 2, statusCode: 503, data: Data())
        await listPage.value

        XCTAssertEqual(viewModel.failedConversationListLoad?.operation, .conversations(
            reset: false,
            after: "list-page-cursor"
        ))
        XCTAssertNotNil(viewModel.failedConversationHistoryLoad)
        XCTAssertTrue(viewModel.transcriptLoadFailed)

        await viewModel.retryConversationLoad()

        XCTAssertNil(viewModel.failedConversationListLoad)
        XCTAssertNotNil(viewModel.failedConversationHistoryLoad)
        XCTAssertEqual(viewModel.transcriptLoadFailure, .messages(
            conversationID: firstID,
            reset: true,
            after: nil
        ))
        XCTAssertTrue(viewModel.transcriptLoadFailed)
        XCTAssertFalse(viewModel.canSendTranscriptMessage)

        await viewModel.retryConversationLoad()

        XCTAssertNil(viewModel.failedConversationHistoryLoad)
        XCTAssertFalse(viewModel.transcriptLoadFailed)
        XCTAssertTrue(viewModel.canSendTranscriptMessage)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: historyPath).count, 2)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: listPath).count, 3)
        XCTAssertEqual(viewModel.messages.first?.content, "Hi")
    }

    private func enqueueConcurrentFailures(
        on recorder: AndroidTranscriptRecorder,
        firstID: String,
        secondID: String
    ) throws {
        let listPath = "/api/v1/my/conversations"
        let historyPath = "/api/v1/my/conversations/\(firstID)/messages"
        recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: firstID,
                title: "First",
                hasNextPage: true,
                endCursor: "list-page-cursor"
            )
        )
        recorder.enqueue(
            path: listPath,
            statusCode: 503,
            data: Data(#"{"message":"list unavailable"}"#.utf8),
            held: true
        )
        recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(id: secondID, title: "Second")
        )
        recorder.enqueue(
            path: historyPath,
            statusCode: 503,
            data: Data(#"{"message":"history unavailable"}"#.utf8),
            held: true
        )
        try recorder.enqueue(path: historyPath, data: AndroidTranscriptFixtures.data("native.chat.page-1"))
    }
}
