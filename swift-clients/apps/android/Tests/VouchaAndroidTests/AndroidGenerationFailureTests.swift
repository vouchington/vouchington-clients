import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidGenerationFailureTests: XCTestCase {
    func testFailedGenerationAfterSwitchRemainsRetryableOnOriginWithoutReplacingNewerDraft() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        enqueueOriginRetry(on: fixture.recorder, firstID: firstID, secondID: secondID)

        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Exact original question"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        await viewModel.selectConversation(secondID)
        viewModel.draft = "A newer draft for the other conversation"
        await runtime.fail("generation unavailable")
        await send.value

        XCTAssertEqual(viewModel.selectedConversationID, secondID)
        XCTAssertEqual(viewModel.draft, "A newer draft for the other conversation")
        XCTAssertEqual(viewModel.failedGenerations[firstID]?.text, "Exact original question")
        XCTAssertNotNil(viewModel.failedGenerations[firstID]?.message)
        XCTAssertTrue(fixture.recorder.requestSnapshot().allSatisfy {
            $0.url?.path != "/api/v1/conversations/\(firstID)/client-generated-chat"
        })

        await viewModel.selectConversation(firstID)
        XCTAssertEqual(viewModel.draft, "A newer draft for the other conversation")
        XCTAssertNotNil(viewModel.failedGenerationForSelectedConversation)
        XCTAssertTrue(viewModel.errorMessage?.isEmpty == false)

        await viewModel.retryFailedGeneration()

        let post = try XCTUnwrap(fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(firstID)/client-generated-chat"
        ).last)
        let body = try XCTUnwrap(post.httpBody).jsonObject()
        XCTAssertEqual(body["message"] as? String, "Exact original question")
        XCTAssertEqual(viewModel.draft, "A newer draft for the other conversation")
        XCTAssertNil(viewModel.failedGenerations[firstID])
        XCTAssertNil(viewModel.pendingTurns[firstID])
        let generationCount = await runtime.generationCount()
        XCTAssertEqual(generationCount, 2)
    }

    func testFailedGenerationPreservesNewerDraftTypedInSameConversation() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            conversationID: conversationID,
            userText: "Original submitted text",
            assistantText: "Retry answer"
        )

        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original submitted text"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        viewModel.draft = "Newer text typed during generation"
        await runtime.fail("generation unavailable")
        await send.value

        XCTAssertEqual(viewModel.draft, "Newer text typed during generation")
        XCTAssertEqual(viewModel.failedGenerations[conversationID]?.text, "Original submitted text")
        XCTAssertNotNil(viewModel.failedGenerationForSelectedConversation)
        XCTAssertTrue(viewModel.errorMessage?.isEmpty == false)

        await viewModel.retryFailedGeneration()

        let post = try XCTUnwrap(fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(conversationID)/client-generated-chat"
        ).last)
        let body = try XCTUnwrap(post.httpBody).jsonObject()
        XCTAssertEqual(body["message"] as? String, "Original submitted text")
        XCTAssertEqual(viewModel.draft, "Newer text typed during generation")
        XCTAssertNil(viewModel.failedGenerations[conversationID])
        XCTAssertNil(viewModel.pendingTurns[conversationID])
    }

    private func enqueueOriginRetry(on recorder: AndroidTranscriptRecorder, firstID: String, secondID: String) {
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: recorder,
            firstID: firstID,
            secondID: secondID
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: recorder,
            conversationID: firstID,
            userText: "Exact original question",
            assistantText: "Retry answer"
        )
    }
}
