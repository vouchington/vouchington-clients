import Foundation
@testable import VouchaAndroid
import VouchaLocalization
import XCTest

@MainActor
final class AndroidModelTranscriptErrorRecoveryTests: XCTestCase {
    func testPendingErrorSurvivesSuccessfulStatusCheckAndDownload() async throws {
        let setup = try await makePendingSetup(runtime: FakeAICoreRuntime(
            state: .available,
            response: "Generated answer"
        ))
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let error = try XCTUnwrap(setup.viewModel.errorMessage)
        await setup.viewModel.refreshStatus()
        XCTAssertEqual(setup.viewModel.errorMessage, error)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        await setup.viewModel.downloadModel()
        XCTAssertEqual(setup.viewModel.errorMessage, error)
        XCTAssertEqual(setup.viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        XCTAssertEqual(setup.viewModel.pendingTurnForSelectedConversation, setup.pending)
        XCTAssertEqual(setup.fixture.recorder.requestSnapshot(path: savePath).count, 1)
    }

    func testGenerationErrorSurvivesSuccessfulModelOperations() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: FakeAICoreRuntime(state: .available, error: TestError.failed)
        )
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Keep the failed generation"
        await viewModel.sendOnDevice()
        let error = try XCTUnwrap(viewModel.errorMessage)
        await viewModel.refreshStatus()
        XCTAssertEqual(viewModel.errorMessage, error)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        await viewModel.downloadModel()
        XCTAssertEqual(viewModel.errorMessage, error)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        XCTAssertEqual(viewModel.draft, "Keep the failed generation")
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: savePath).count, 0)
    }

    func testCreationErrorSurvivesCancelledDownload() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        fixture.recorder.enqueue(path: "/api/v1/conversations", statusCode: 503, data: Data("{}".utf8))
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(
            runtime: ModelOperationRuntime(failsStatus: false, cancelsDownload: true)
        )
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        let created = await viewModel.createConversation()
        XCTAssertFalse(created)
        let error = try XCTUnwrap(viewModel.errorMessage)
        await viewModel.downloadModel()
        XCTAssertEqual(viewModel.modelState, .downloadable)
        XCTAssertEqual(viewModel.errorMessage, error)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToCreateConversation)
        XCTAssertTrue(viewModel.conversationCreationFailed)
    }

    func testSuccessfulPendingRetryRevealsRetainedModelFailure() async throws {
        let setup = try await makePendingSetup(runtime: ModelOperationRuntime(
            failsStatus: true,
            cancelsDownload: false
        ))
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let viewModel = setup.viewModel
        let saveError = try XCTUnwrap(viewModel.errorMessage)
        await viewModel.refreshStatus()
        let modelError = try XCTUnwrap(viewModel.modelErrorMessage)
        XCTAssertEqual(viewModel.modelState, .unavailable)
        XCTAssertEqual(viewModel.errorMessage, saveError)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToPersistOnDeviceResponse)
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: setup.fixture.recorder,
            conversationID: setup.pending.conversationID,
            userText: setup.pending.userText,
            assistantText: setup.pending.assistantText
        )
        await viewModel.retryPendingTurn()
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.errorMessage, modelError)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        XCTAssertFalse(viewModel.canSendTranscriptMessage)
        XCTAssertEqual(setup.fixture.recorder.requestSnapshot(path: savePath).count, 2)
        await viewModel.downloadModel()
        XCTAssertEqual(viewModel.modelState, .available)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.modelErrorMessage)
        XCTAssertNil(viewModel.chatErrorMessageKey)
    }

    func testPendingRetryDoesNotReplaceNewerSettingsErrorWithModelError() async throws {
        let setup = try await makePendingSetup(runtime: ModelOperationRuntime(
            failsStatus: true,
            cancelsDownload: false
        ))
        defer { AndroidTranscriptURLProtocol.uninstall(host: setup.fixture.host) }
        let viewModel = setup.viewModel
        await viewModel.refreshStatus()
        setup.fixture.recorder.enqueue(
            path: savePath, held: true,
            responseBuilder: AndroidTranscriptTestFactory.generatedChatResponseBuilder(
                conversationID: setup.pending.conversationID,
                userText: setup.pending.userText,
                assistantText: setup.pending.assistantText
            )
        )
        let retry = Task { await viewModel.retryPendingTurn() }
        _ = try await setup.fixture.recorder.waitForRequest(path: savePath, occurrence: 2)
        viewModel.errorMessage = "Later settings failure"
        setup.fixture.recorder.completeHeld(
            path: savePath, occurrence: 2, statusCode: 200,
            data: AndroidTranscriptTestFactory.generatedChatResponse(
                conversationID: setup.pending.conversationID,
                userText: setup.pending.userText,
                assistantText: setup.pending.assistantText,
                userMessageID: setup.pending.userMessageID,
                assistantMessageID: setup.pending.assistantMessageID
            )
        )
        await retry.value
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(viewModel.errorMessage, "Later settings failure")
        XCTAssertEqual(viewModel.chatErrorKind, .other)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)
    }

    private struct PendingSetup {
        let fixture: AndroidTranscriptTestClient
        let viewModel: ViewModel
        let pending: AndroidPendingChatTurn
    }

    private var savePath: String {
        "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
    }

    private func makePendingSetup(
        runtime: any AndroidAICoreRunning
    ) async throws -> PendingSetup {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        fixture.recorder.enqueue(path: savePath, statusCode: 503, data: Data("{}".utf8))
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Keep exact pending payload"
        await viewModel.sendOnDevice()
        let pending = try XCTUnwrap(viewModel.pendingTurnForSelectedConversation)
        return PendingSetup(fixture: fixture, viewModel: viewModel, pending: pending)
    }
}

private struct ModelOperationRuntime: AndroidAICoreRunning {
    let failsStatus: Bool
    let cancelsDownload: Bool

    func status() async throws -> AndroidModelState {
        if failsStatus { throw TestError.failed }
        return .available
    }

    func download() async throws -> AndroidModelState {
        if cancelsDownload { throw CancellationError() }
        return .available
    }

    func generate(prompt _: String) async throws -> String {
        "Generated answer"
    }
}
