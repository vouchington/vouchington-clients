import Foundation
@testable import VouchaAndroid
import VouchaAPI
import VouchaCore
import XCTest

@MainActor
final class AndroidDraftBackedFailureLifecycleTests: XCTestCase {
    func testSuccessfulResubmissionClearsDeferredFailureBeforeConversationSwitch() async throws {
        let scenario = try await makeFailedDraftScenario()
        defer { AndroidTranscriptURLProtocol.uninstall(host: scenario.fixture.host) }
        let conversationID = AndroidTranscriptTestFactory.newConversationID
        let otherID = "0198ffff-0000-7000-8000-000000000003"
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: scenario.fixture.recorder,
            conversationID: conversationID,
            userText: "Edited and resubmitted",
            assistantText: "Retry answer"
        )
        scenario.fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(otherID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        scenario.fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(conversationID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )

        scenario.viewModel.draft = "Edited and resubmitted"
        await scenario.viewModel.sendOnDevice()
        XCTAssertNil(scenario.viewModel.draftBackedGenerationFailures[conversationID])
        XCTAssertNil(scenario.viewModel.generationErrors[conversationID])
        XCTAssertEqual(
            scenario.fixture.recorder.requestSnapshot(
                path: "/api/v1/conversations/\(conversationID)/client-generated-chat"
            ).count,
            1
        )

        await scenario.viewModel.selectConversation(otherID)
        await scenario.viewModel.selectConversation(conversationID)
        XCTAssertNil(scenario.viewModel.failedGenerationForSelectedConversation)
        XCTAssertNil(scenario.viewModel.chatErrorMessageKey)
    }

    func testAccountTransitionClearsDeferredFailureForReusedConversationID() async throws {
        let scenario = try await makeFailedDraftScenario()
        let secondFixture = try AndroidTranscriptTestFactory.makeClient()
        defer {
            AndroidTranscriptURLProtocol.uninstall(host: scenario.fixture.host)
            AndroidTranscriptURLProtocol.uninstall(host: secondFixture.host)
        }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: secondFixture.recorder)

        await scenario.viewModel.bindAccount(client: secondFixture.client, userID: "another-user")

        XCTAssertEqual(scenario.viewModel.selectedConversationID, AndroidTranscriptTestFactory.newConversationID)
        XCTAssertTrue(scenario.viewModel.draftBackedGenerationFailures.isEmpty)
        XCTAssertTrue(scenario.viewModel.failedGenerations.isEmpty)
        XCTAssertTrue(scenario.viewModel.generationErrors.isEmpty)
        XCTAssertNil(scenario.viewModel.chatErrorMessageKey)
    }

    private func makeFailedDraftScenario() async throws -> DraftBackedFailureScenario {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        do {
            try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        } catch {
            AndroidTranscriptURLProtocol.uninstall(host: fixture.host)
            throw error
        }
        let runtime = DelayedTranscriptRuntime()
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: runtime)
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Original failed draft"
        let send = Task { await viewModel.sendOnDevice() }
        do {
            try await runtime.waitUntilStarted()
        } catch {
            AndroidTranscriptURLProtocol.uninstall(host: fixture.host)
            throw error
        }
        await runtime.fail("generation unavailable")
        await send.value
        return DraftBackedFailureScenario(fixture: fixture, viewModel: viewModel)
    }
}

private struct DraftBackedFailureScenario {
    let fixture: AndroidTranscriptTestClient
    let viewModel: ViewModel
}
