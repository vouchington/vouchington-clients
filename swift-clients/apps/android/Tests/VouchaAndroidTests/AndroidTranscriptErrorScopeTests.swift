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
final class AndroidTranscriptErrorScopeTests: XCTestCase {
    func testPendingRetryFailurePreservesSelectedConversationError() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        let firstHistory = "/api/v1/my/conversations/\(firstID)/messages"
        let secondHistory = "/api/v1/my/conversations/\(secondID)/messages"
        let retryPath = "/api/v1/conversations/\(firstID)/client-generated-chat"
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: AndroidTranscriptTestFactory.twoConversationListResponse(
                firstID: firstID,
                secondID: secondID
            )
        )
        fixture.recorder.enqueue(path: firstHistory, data: AndroidTranscriptTestFactory.emptyMessagePageResponse())
        fixture.recorder.enqueue(path: secondHistory, statusCode: 503, data: Data(#"{"message":"offline"}"#.utf8))
        fixture.recorder.enqueue(
            path: retryPath,
            statusCode: 503,
            data: Data(#"{"message":"retry failed"}"#.utf8),
            held: true
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.pendingTurns[firstID] = AndroidPendingChatTurn(
            conversationID: firstID,
            userMessageID: "0198ffff-0001-7000-8000-000000000001",
            assistantMessageID: "0198ffff-0001-7000-8000-000000000002",
            userText: "Saved question",
            assistantText: "Saved answer",
            modelProvider: "android_aicore",
            modelName: "android-aicore-system"
        )

        let retry = Task { await viewModel.retryPendingTurn() }
        _ = try await fixture.recorder.waitForRequest(path: retryPath, occurrence: 1)
        await viewModel.selectConversation(secondID)
        let selectedError = try XCTUnwrap(viewModel.errorMessage)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)

        fixture.recorder.completeHeld(path: retryPath, occurrence: 1, statusCode: 503, data: Data())
        await retry.value

        XCTAssertEqual(viewModel.selectedConversationID, secondID)
        XCTAssertEqual(viewModel.errorMessage, selectedError)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)
        XCTAssertNotNil(viewModel.pendingTurns[firstID])
        XCTAssertNil(viewModel.pendingTurnForSelectedConversation)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: firstHistory).count, 1)
        XCTAssertEqual(fixture.recorder.requestSnapshot(path: secondHistory).count, 1)
    }

    func testConversationCreationFailureUsesCreationErrorCopyAndClearsOnSuccess() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let createPath = "/api/v1/conversations"
        let newID = "0198ffff-0000-7000-8000-000000000003"
        fixture.recorder.enqueue(path: createPath, statusCode: 503, data: Data(#"{"message":"offline"}"#.utf8))
        fixture.recorder.enqueue(path: createPath, data: AndroidTranscriptTestFactory.conversationResponse(
            id: newID,
            title: "Recovered conversation"
        ))
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(newID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )

        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: FakeAICoreRuntime(state: .available))
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        await viewModel.createConversation()

        XCTAssertTrue(viewModel.conversationCreationFailed)
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftChatUnableToCreateConversation)

        await viewModel.createConversation()

        XCTAssertFalse(viewModel.conversationCreationFailed)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.chatErrorMessageKey)
        XCTAssertEqual(viewModel.selectedConversationID, newID)
    }

    func testGenerationFailureRestoredToEmptyDraftKeepsGenerationCopy() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Restore this failed question"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        await runtime.fail("generation unavailable")
        await send.value
        XCTAssertEqual(viewModel.draft, "Restore this failed question")
        XCTAssertNil(viewModel.failedGenerationForSelectedConversation)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidGenerationFailed)
        viewModel.errorMessage = "A later settings operation failed"
        XCTAssertEqual(viewModel.chatErrorKind, .other)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)
        viewModel.resetForAccountTransition()
        XCTAssertEqual(viewModel.chatErrorKind, .other)
        XCTAssertNil(viewModel.chatErrorMessageKey)
    }

}
