import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidDraftBackedFailurePromotionTests: XCTestCase {
    func testFailurePromotesOnLeavingAndRetriesOriginalTextOnReturn() async throws {
        let scenario = try await makeScenario()
        defer { AndroidTranscriptURLProtocol.uninstall(host: scenario.fixture.host) }
        let viewModel = scenario.viewModel

        XCTAssertEqual(viewModel.draft, "Original failed draft")
        XCTAssertNil(viewModel.failedGenerations[scenario.firstID])
        XCTAssertTrue(viewModel.canSendTranscriptMessage)
        viewModel.draft = "An edit made after the failed attempt"
        await viewModel.selectConversation(scenario.secondID)
        XCTAssertEqual(viewModel.failedGenerations[scenario.firstID]?.text, "Original failed draft")
        XCTAssertEqual(viewModel.draft, "An edit made after the failed attempt")
        viewModel.draft = "New conversation draft"
        await viewModel.sendOnDevice()
        XCTAssertEqual(viewModel.draft, "")

        await viewModel.selectConversation(scenario.firstID)
        XCTAssertEqual(viewModel.failedGenerationForSelectedConversation?.text, "Original failed draft")
        await viewModel.retryFailedGeneration()

        let post = try XCTUnwrap(scenario.fixture.recorder.requestSnapshot(
            path: "/api/v1/conversations/\(scenario.firstID)/client-generated-chat"
        ).first)
        XCTAssertEqual(try XCTUnwrap(post.httpBody).jsonObject()["message"] as? String, "Original failed draft")
        XCTAssertNil(viewModel.failedGenerations[scenario.firstID])
        XCTAssertNil(viewModel.pendingTurns[scenario.firstID])
        let generationCount = await scenario.runtime.generationCount()
        XCTAssertEqual(generationCount, 3)
    }

    private func makeScenario() async throws -> DraftBackedFailureScenario {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        let firstID = AndroidTranscriptTestFactory.newConversationID
        let secondID = "0198ffff-0000-7000-8000-000000000003"
        AndroidTranscriptTestFactory.enqueueConversationSwitchFlow(
            on: fixture.recorder,
            firstID: firstID,
            secondID: secondID
        )
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(firstID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            conversationID: secondID,
            userText: "New conversation draft",
            assistantText: "Retry answer"
        )
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            conversationID: firstID,
            userText: "Original failed draft",
            assistantText: "Retry answer"
        )
        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original failed draft"
        let send = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        await runtime.fail("generation unavailable")
        await send.value
        return DraftBackedFailureScenario(
            fixture: fixture,
            runtime: runtime,
            viewModel: viewModel,
            firstID: firstID,
            secondID: secondID
        )
    }
}

private struct DraftBackedFailureScenario {
    let fixture: AndroidTranscriptTestClient
    let runtime: DelayedTranscriptRuntime
    let viewModel: ViewModel
    let firstID: String
    let secondID: String
}
