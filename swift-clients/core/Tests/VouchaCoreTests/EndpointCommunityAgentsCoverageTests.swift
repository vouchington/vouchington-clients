import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class EndpointCommunityAgentsCoverageTests: XCTestCase {
    func testCommunityAgentEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.communityAiAgents(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/ai-agents"
        )
        assertEndpoint(
            Endpoint.enableCommunityAiAgent(idOrSlug: "test community", agentSlug: "mod-bot"),
            method: .PUT,
            path: "/api/v1/communities/test%20community/ai-agents/mod-bot"
        )
        assertEndpoint(
            Endpoint.disableCommunityAiAgent(idOrSlug: "test community", agentSlug: "mod-bot"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/ai-agents/mod-bot"
        )
        assertEndpoint(
            Endpoint.communityAgentPrompts(idOrSlug: "test community"),
            path: "/api/v1/communities/test%20community/agent-prompts"
        )
        assertEndpoint(
            Endpoint.communityAgentPrompt(idOrSlug: "test community", promptId: "prompt-1"),
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1"
        )
        assertEndpoint(
            Endpoint.createCommunityAgentPrompt(
                idOrSlug: "test community",
                prompt: "Be concise",
                modelName: "gpt-4.1",
                modelProvider: "openai"
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/agent-prompts",
            body: [
                "prompt": "Be concise",
                "model_name": "gpt-4.1",
                "model_provider": "openai"
            ]
        )
        assertEndpoint(
            Endpoint.updateCommunityAgentPrompt(
                idOrSlug: "test community",
                promptId: "prompt-1",
                prompt: "Be helpful"
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1",
            body: ["prompt": "Be helpful"]
        )
        assertEndpoint(
            Endpoint.deleteCommunityAgentPrompt(idOrSlug: "test community", promptId: "prompt-1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1"
        )
        assertEndpoint(
            Endpoint.allocateCommunityAgentPromptSlot(idOrSlug: "test community", promptId: "prompt-1"),
            method: .POST,
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1/allocations"
        )
        assertEndpoint(
            Endpoint.deallocateCommunityAgentPromptSlot(idOrSlug: "test community", promptId: "prompt-1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1/allocations"
        )
        assertEndpoint(
            Endpoint.communityAgentPromptHistory(idOrSlug: "test community", promptId: "prompt-1", before: "cursor-1"),
            path: "/api/v1/communities/test%20community/agent-prompts/history"
        )
        XCTAssertEqual(
            Endpoint.communityAgentPromptHistory(idOrSlug: "test community", promptId: "prompt-1", before: "cursor-1")
                .queryItems,
            [
                URLQueryItem(name: "promptId", value: "prompt-1"),
                URLQueryItem(name: "before", value: "cursor-1")
            ]
        )
        assertEndpoint(
            Endpoint.testCommunityAgentPrompt(
                idOrSlug: "test community",
                promptId: "prompt-1",
                text: "Please review this post",
                saveForTraining: true,
                expectedFlagged: false
            ),
            method: .POST,
            path: "/api/v1/communities/test%20community/agent-prompts/prompt-1/test-runs",
            body: [
                "text": "Please review this post",
                "save_for_training": true,
                "expected_flagged": false
            ]
        )
    }

    func testCommunityApplicationAndInviteEndpointsUseExpectedRoutesAndBodies() {
        assertEndpoint(
            Endpoint.approveCommunityApplication(idOrSlug: "test community", applicationId: "application-1"),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/applications/application-1",
            body: ["status": "approved"]
        )
        assertEndpoint(
            Endpoint.rejectCommunityApplication(
                idOrSlug: "test community",
                applicationId: "application-1",
                rejectionReason: "No thanks"
            ),
            method: .PATCH,
            path: "/api/v1/communities/test%20community/applications/application-1",
            body: ["status": "rejected", "reason": "No thanks"]
        )
        assertEndpoint(
            Endpoint.sendCommunityInvite(idOrSlug: "test community", email: "person@example.com", username: "person"),
            method: .POST,
            path: "/api/v1/communities/test%20community/invites",
            body: ["email": "person@example.com", "username": "person"]
        )
        assertEndpoint(
            Endpoint.revokeCommunityInvite(idOrSlug: "test community", inviteId: "invite-1"),
            method: .DELETE,
            path: "/api/v1/communities/test%20community/invites/invite-1"
        )
        assertEndpoint(
            Endpoint.redeemCommunityInviteCode(code: "code-123"),
            method: .POST,
            path: "/api/v1/communities/invite-redemptions",
            body: ["code": "code-123"]
        )
    }

    func testCommunityListItemTypeRequestBodyFieldsMatchBackendContract() {
        XCTAssertEqual(CommunityListItemType.topic.requestBodyField, "topic_id")
        XCTAssertEqual(CommunityListItemType.rssFeed.requestBodyField, "rss_feed_id")
        XCTAssertEqual(CommunityListItemType.post.requestBodyField, "post_id")
        XCTAssertEqual(CommunityListItemType.urlHostname.requestBodyField, "url_hostname_id")
        XCTAssertEqual(CommunityListItemType.url.requestBodyField, "url_id")
    }
}
