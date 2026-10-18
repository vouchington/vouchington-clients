import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidTranscriptOperationErrorTests: XCTestCase {
    func testCreationFailureSurvivesConcurrentSuccessfulHistoryLoad() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let listPath = "/api/v1/my/conversations"
        let historyPath = "/api/v1/my/conversations/\(conversationID)/messages"
        fixture.recorder.enqueue(
            path: listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(id: conversationID, title: "Chat")
        )
        fixture.recorder.enqueue(
            path: historyPath,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse(),
            held: true
        )
        fixture.recorder.enqueue(
            path: "/api/v1/conversations",
            statusCode: 503,
            data: Data(#"{"message":"creation unavailable"}"#.utf8)
        )
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, response: "Unused")
        )
        let bind = Task { await viewModel.bindAccount(client: fixture.client, userID: "fixture-user") }
        _ = try await fixture.recorder.waitForRequest(path: historyPath, occurrence: 1)

        let created = await viewModel.createConversation()
        XCTAssertFalse(created)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToCreateConversation)

        fixture.recorder.completeHeld(
            path: historyPath,
            occurrence: 1,
            statusCode: 200,
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await bind.value

        XCTAssertEqual(viewModel.loadedConversationHistoryID, conversationID)
        XCTAssertTrue(viewModel.conversationCreationFailed)
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToCreateConversation)
    }

    func testCreationFailureCopyOutranksUnrelatedListFailure() async throws {
        let setup = try await makePendingListRetrySetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        setup.fixture.recorder.enqueue(
            path: "/api/v1/conversations",
            statusCode: 503,
            data: Data(#"{"message":"creation unavailable"}"#.utf8)
        )

        let created = await setup.viewModel.createConversation()

        XCTAssertFalse(created)
        XCTAssertNotNil(setup.viewModel.failedConversationListLoad)
        XCTAssertTrue(setup.viewModel.conversationCreationFailed)
        XCTAssertEqual(setup.viewModel.errorMessage, setup.viewModel.conversationCreationErrorMessage)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToCreateConversation)
    }

    func testPendingRetryKeepsListFailureAvailableForItsOwnRetry() async throws {
        let setup = try await makePendingListRetrySetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let viewModel = setup.viewModel
        XCTAssertNotNil(viewModel.failedConversationListLoad)

        viewModel.draft = "Retry this save"
        await viewModel.sendOnDevice()
        XCTAssertNotNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)

        await viewModel.retryPendingTurn()
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertNotNil(viewModel.failedConversationListLoad)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)

        setup.fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: setup.conversationID,
                title: "Chat",
                hasNextPage: false
            )
        )
        await viewModel.retryConversationLoad()

        XCTAssertNil(viewModel.failedConversationListLoad)
        XCTAssertFalse(viewModel.transcriptLoadFailed)
        XCTAssertNil(viewModel.chatErrorMessageKey)
        let listRequests = setup.fixture.recorder.requestSnapshot(path: "/api/v1/my/conversations")
        XCTAssertEqual(listRequests.count, 3)
        let retriedListURL = try XCTUnwrap(listRequests.last?.url)
        let listQuery = try XCTUnwrap(URLComponents(url: retriedListURL, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertEqual(listQuery.first(where: { $0.name == "after" })?.value, "next-page")

        let saveRequests = setup.fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(setup.conversationID)/client-generated-chat"
        )
        XCTAssertEqual(saveRequests.count, 2)
        let firstSave = try XCTUnwrap(saveRequests.first?.httpBody).jsonObject()
        let retriedSave = try XCTUnwrap(saveRequests.last?.httpBody).jsonObject()
        XCTAssertEqual(retriedSave["user_message_id"] as? String, firstSave["user_message_id"] as? String)
        XCTAssertEqual(retriedSave["assistant_message_id"] as? String, firstSave["assistant_message_id"] as? String)
        XCTAssertEqual(retriedSave["message"] as? String, firstSave["message"] as? String)
    }

}

private struct PendingListRetrySetup {
    let fixture: AndroidTranscriptTestClient
    let viewModel: ViewModel
    let conversationID: String
}

@MainActor
private func makePendingListRetrySetup() async throws -> PendingListRetrySetup {
    let fixture = try AndroidTranscriptTestFactory.makeClient()
    let conversationID = AndroidTranscriptTestFactory.newConversationID
    let listPath = "/api/v1/my/conversations"
    fixture.recorder.enqueue(
        path: listPath,
        data: AndroidTranscriptTestFactory.conversationListResponse(
            id: conversationID,
            title: "Chat",
            hasNextPage: true,
            endCursor: "next-page"
        )
    )
    fixture.recorder.enqueue(
        path: "/api/v1/my/conversations/\(conversationID)/messages",
        data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
    )
    fixture.recorder.enqueue(
        path: listPath,
        statusCode: 503,
        data: Data(#"{"message":"list unavailable"}"#.utf8)
    )
    fixture.recorder.enqueue(
        path: "/api/v1/conversations/\(conversationID)/client-generated-chat",
        statusCode: 503,
        data: Data(#"{"message":"save unavailable"}"#.utf8)
    )
    AndroidTranscriptTestFactory.enqueueGeneratedTurn(
        on: fixture.recorder,
        conversationID: conversationID,
        userText: "Retry this save",
        assistantText: "Keep this answer"
    )
    let viewModel = AndroidTranscriptTestFactory.makeViewModel(
        runtime: FakeAICoreRuntime(state: .available, response: "Keep this answer")
    )
    viewModel.modelState = .available
    await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
    await viewModel.loadConversationPage(reset: false)
    return PendingListRetrySetup(fixture: fixture, viewModel: viewModel, conversationID: conversationID)
}
