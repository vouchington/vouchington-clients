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
final class AndroidTranscriptGenerationErrorTests: XCTestCase {
    func testLateListFailureKeepsEarlierGenerationFailureVisible() async throws {
        let setup = try await makeGenerationListRetrySetup(listHeld: true)
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        try await setup.runtime.waitUntilStarted()
        let pagination = Task { await setup.viewModel.loadConversationPage(reset: false) }
        _ = try await setup.fixture.recorder.waitForRequest(path: setup.listPath, occurrence: 2)
        setup.viewModel.draft = "Newer draft"
        await setup.runtime.fail("generation unavailable")
        await setup.send.value
        let generationError = try XCTUnwrap(setup.viewModel.errorMessage)
        setup.fixture.recorder.completeHeld(
            path: setup.listPath, occurrence: 2, statusCode: 503, data: Data("{}".utf8)
        )
        await pagination.value

        XCTAssertNotNil(setup.viewModel.failedConversationListLoad)
        XCTAssertNotNil(setup.viewModel.failedGenerationForSelectedConversation)
        XCTAssertEqual(setup.viewModel.errorMessage, generationError)
        XCTAssertEqual(setup.viewModel.chatErrorKind, .generation)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        XCTAssertEqual(setup.viewModel.draft, "Newer draft")
    }

    func testDraftBackedGenerationErrorSurvivesListRetry() async throws {
        let setup = try await makeGenerationListRetrySetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        try await setup.runtime.waitUntilStarted()
        await setup.viewModel.loadConversationPage(reset: false)
        await setup.runtime.fail("generation unavailable")
        await setup.send.value

        XCTAssertNil(setup.viewModel.failedGenerationForSelectedConversation)
        XCTAssertEqual(setup.viewModel.draft, "Retry this generation")
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        let generationError = try XCTUnwrap(setup.viewModel.errorMessage)
        XCTAssertEqual(setup.viewModel.chatErrorKind, .generation)

        setup.fixture.recorder.enqueue(
            path: setup.listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: setup.conversationID,
                title: "Chat",
                hasNextPage: false
            )
        )
        await setup.viewModel.retryConversationLoad()

        XCTAssertNil(setup.viewModel.failedConversationListLoad)
        XCTAssertEqual(setup.viewModel.draft, "Retry this generation")
        XCTAssertEqual(setup.viewModel.errorMessage, generationError)
        XCTAssertEqual(setup.viewModel.chatErrorKind, .generation)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
    }

    func testGenerationRetryUsesGenerationCopyBeforeListFailureThenRestoresListRetry() async throws {
        let setup = try await makeGenerationListRetrySetup()
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        try await setup.runtime.waitUntilStarted()
        await setup.viewModel.loadConversationPage(reset: false)
        XCTAssertNotNil(setup.viewModel.failedConversationListLoad)

        setup.viewModel.draft = "Newer draft"
        await setup.runtime.fail("generation unavailable")
        await setup.send.value

        let failedGeneration = try XCTUnwrap(setup.viewModel.failedGenerationForSelectedConversation)
        XCTAssertEqual(setup.viewModel.draft, "Newer draft")
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        XCTAssertEqual(setup.viewModel.errorMessage, failedGeneration.message)

        await setup.viewModel.retryFailedGeneration()

        XCTAssertNil(setup.viewModel.failedGenerationForSelectedConversation)
        XCTAssertNil(setup.viewModel.pendingTurnForSelectedConversation)
        XCTAssertNil(setup.viewModel.generationErrorForSelectedConversation)
        XCTAssertEqual(setup.viewModel.draft, "Newer draft")
        XCTAssertNotNil(setup.viewModel.failedConversationListLoad)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)
        XCTAssertEqual(setup.fixture.recorder.requestSnapshot(path: setup.savePath).count, 1)

        setup.fixture.recorder.enqueue(
            path: setup.listPath,
            data: AndroidTranscriptTestFactory.conversationListResponse(
                id: setup.conversationID,
                title: "Chat",
                hasNextPage: false
            )
        )
        await setup.viewModel.retryConversationLoad()

        XCTAssertNil(setup.viewModel.failedConversationListLoad)
        XCTAssertNil(setup.viewModel.chatErrorMessageKey)
        let listRequests = setup.fixture.recorder.requestSnapshot(path: setup.listPath)
        XCTAssertEqual(listRequests.count, 3)
        let retryURL = try XCTUnwrap(listRequests.last?.url)
        let queryItems = try XCTUnwrap(URLComponents(url: retryURL, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertEqual(queryItems.first(where: { $0.name == "after" })?.value, "next-page")
    }
}

@MainActor
private struct GenerationListRetrySetup {
    let fixture: AndroidTranscriptTestClient
    let runtime: DelayedTranscriptRuntime
    let viewModel: ViewModel
    let send: Task<Void, Never>
    let conversationID: String
    let listPath = "/api/v1/my/conversations"
    var savePath: String {
        "/api/v1/conversations/\(conversationID)/client-generated-chat"
    }
}

@MainActor
private func makeGenerationListRetrySetup(listHeld: Bool = false) async throws -> GenerationListRetrySetup {
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
        data: Data(#"{"message":"list unavailable"}"#.utf8),
        held: listHeld
    )
    AndroidTranscriptTestFactory.enqueueGeneratedTurn(
        on: fixture.recorder,
        conversationID: conversationID,
        userText: "Retry this generation",
        assistantText: "Retry answer"
    )

    let runtime = DelayedTranscriptRuntime()
    let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
    viewModel.modelState = .available
    await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
    viewModel.draft = "Retry this generation"
    let send = Task { await viewModel.sendOnDevice() }
    return GenerationListRetrySetup(
        fixture: fixture,
        runtime: runtime,
        viewModel: viewModel,
        send: send,
        conversationID: conversationID
    )
}
