import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

extension CommunityDetailActionTests {
    func testModerationTabHydratesAndReloadsVacationDigestPreference() async throws {
        registerCommunityAdminFixtures()
        let path = "/api/v1/communities/builders/moderator-vacation"
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"vacation":null,"should_suppress_community_digests_while_on_vacation":true}"#.utf8),
            200
        )
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )

        await viewModel.load()
        XCTAssertTrue(viewModel.suppressCommunityDigestsWhileOnVacation)

        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"should_suppress_community_digests_while_on_vacation":false}"#.utf8), 200, 0),
            (Data(#"{"vacation":null,"should_suppress_community_digests_while_on_vacation":false}"#.utf8), 200, 0)
        ]
        await viewModel.setSuppressCommunityDigestsWhileOnVacation(false)

        XCTAssertFalse(viewModel.suppressCommunityDigestsWhileOnVacation)
        XCTAssertEqual(
            viewModel.statusMessage,
            UiMessage(.nativeSwiftCommunityStatusCommunityDigestsResumed)
        )
    }

    func testAgentPromptAutomodAndVacationActionsCallCommunityEndpoints() async throws {
        registerCommunityAdminFixtures()
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)

        await viewModel.enableCommunityAiAgent(agentSlug: "mod-bot")
        await viewModel.disableCommunityAiAgent(agentSlug: "mod-bot")
        await viewModel.createCommunityAgentPrompt(
            prompt: "Be concise",
            modelName: "gpt-4.1",
            modelProvider: "openai"
        )
        await viewModel.updateCommunityAgentPrompt(promptId: "prompt-1", prompt: "Be precise")
        await viewModel.allocateCommunityAgentPrompt(promptId: "prompt-1")
        await viewModel.deallocateCommunityAgentPrompt(promptId: "prompt-1")
        await viewModel.deleteCommunityAgentPrompt(promptId: "prompt-1")
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/agent-prompts/prompt-1/test-runs"] = (
            Data(#"{"flagged":false}"#.utf8),
            200
        )
        let flagged = await viewModel.testCommunityAgentPrompt(
            promptId: "prompt-1",
            text: "Please review this post",
            saveForTraining: true,
            expectedFlagged: false
        )
        XCTAssertEqual(flagged, false)
        await viewModel.recordCommunityAutomodFeedback(
            sourceKey: "source-1",
            outcome: .falsePositive,
            action: .reinstate,
            reasonCode: "ok",
            note: "Reviewed"
        )
        await viewModel.loadCommunityAutomodRecentActions()
        let automodRow = try XCTUnwrap(viewModel.summary.rows.first)
        XCTAssertEqual(automodRow.icon, "exclamationmark.triangle")
        XCTAssertEqual(automodRow.title, "Flagged post")
        XCTAssertEqual(automodRow.detail, "Unpublished · 0.94")
        XCTAssertNil(automodRow.declaredLanguage)
        XCTAssertNil(automodRow.detectedLanguage)
        await viewModel.simulateCommunityAutomod(
            promptId: "prompt-1",
            prompt: "Review the content",
            timeWindowHours: 12,
            limit: 5
        )

        viewModel.selectedTab = .settings
        await viewModel.setModeratorVacation(endsAt: Date(timeIntervalSince1970: 1_000))
        await viewModel.setSuppressCommunityDigestsWhileOnVacation(true)
        await viewModel.clearModeratorVacation()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/ai-agents/mod-bot" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/agent-prompts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/agent-prompts/prompt-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/agent-prompts/prompt-1/allocations" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/agent-prompts/prompt-1/test-runs" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/automod/recent-actions/source-1/feedback" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/automod/recent-actions" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/automod/simulate" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/moderator-vacation" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""prompt":"Be concise""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""prompt":"Be precise""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""text":"Please review this post""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""outcome":"false_positive""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""action":"reinstate""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""prompt_id":"prompt-1""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains {
            $0?.contains(#""should_suppress_community_digests_while_on_vacation":true"#) == true
        })
    }
}
