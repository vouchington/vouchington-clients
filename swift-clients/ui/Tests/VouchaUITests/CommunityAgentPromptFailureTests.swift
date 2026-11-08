import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityAgentPromptFailureTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/communities/builders/agent-prompts"

    func testContinuationFailureRetainsRowsAndRetriesSameCursor() async throws {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
        object["page_info"] = ["has_next_page": true, "start_cursor": NSNull(), "end_cursor": "opaque"]
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (JSONSerialization.data(withJSONObject: object), 200, 0),
            (Data("{}".utf8), 503, 0),
            (ApiFixtureLoader.data("web.communities.agent-prompts.default"), 200, 0)
        ]
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        await model.loadMore()
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
        XCTAssertNotNil(model.pagination.lastError)
        XCTAssertEqual(model.slotInfo?.limit, 3)
        await model.loadMore()
        XCTAssertNil(model.pagination.lastError)
        XCTAssertFalse(model.pagination.hasMore)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.suffix(2)
            .allSatisfy { $0.query?.contains("after=opaque") == true })
    }

    func testCodedLimitWithoutMessageUsesLocalizedFallbackAndOtherConflictIsNotLimit() async throws {
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        model.draftPrompt = "Keep draft"
        for (code, limit) in [("COMMUNITY_AGENT_PROMPT_LIMIT_REACHED", true), ("OTHER_CONFLICT", false)] {
            let object = ["code": code]
            CannedFeedURLProtocol.handlers[path] = try (JSONSerialization.data(withJSONObject: object), 409)
            let created = await model.create(prompt: model.draftPrompt)
            XCTAssertFalse(created)
            XCTAssertEqual(model.creationLimitReached, limit)
            XCTAssertEqual(model.draftPrompt, "Keep draft")
            XCTAssertEqual(
                model.notice.map { UiMessages.string($0, locale: .english) },
                UiMessages.string(.nativeSwiftCommunityStatusActionFailed, locale: .english)
            )
        }
    }

    func testHumanCreationLimitMessageIsPreservedWithoutRawCode() async throws {
        CannedFeedURLProtocol.handlers[path] = (
            Data(#"{"code":"COMMUNITY_AGENT_PROMPT_LIMIT_REACHED","message":"Community agent prompt limit reached"}"#
                .utf8), 409
        )
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        let created = await model.create(prompt: "Draft")
        XCTAssertFalse(created)
        XCTAssertTrue(model.creationLimitReached)
        XCTAssertFalse(model.isCreating)
        XCTAssertEqual(
            model.notice.map { UiMessages.string($0, locale: .english) },
            "Community agent prompt limit reached"
        )
    }

    func testSuccessfulCreationRefreshesListAndRetainsDraftWithoutInferringCreationCap() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.agent-prompts.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        object["slot_info"] = ["used": 10, "limit": 10, "remaining": 0, "limits_by_plan": ["plus": 3, "pro": 10]]
        let page = try JSONSerialization.data(withJSONObject: object)
        var refreshedPrompts = try XCTUnwrap(object["community_agent_prompts"] as? [[String: Any]])
        refreshedPrompts[0]["id"] = "created-prompt"
        refreshedPrompts[0]["prompt"] = "Newly created prompt"
        object["community_agent_prompts"] = refreshedPrompts
        object["slot_info"] = ["used": 9, "limit": 10, "remaining": 1, "limits_by_plan": ["plus": 3, "pro": 10]]
        let refreshed = try JSONSerialization.data(withJSONObject: object)
        CannedFeedURLProtocol.queuedHandlers[path] = [(page, 200, 0), (Data("{}".utf8), 200, 0), (refreshed, 200, 0)]
        let parent = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .moderation)
        let model = parent.agentPromptWorkspace
        model.draftPrompt = "Keep draft"
        await model.reload()
        await parent.createCommunityAgentPrompt(prompt: model.draftPrompt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "POST", "GET"])
        XCTAssertEqual(model.slotInfo?.used, 9)
        XCTAssertEqual(model.slotInfo?.remaining, 1)
        XCTAssertEqual(model.pagination.items.map(\.id), ["created-prompt"])
        XCTAssertEqual(model.pagination.items.first?.prompt, "Newly created prompt")
        XCTAssertEqual(model.draftPrompt, "Keep draft")
        XCTAssertNotNil(parent.statusMessage)
        XCTAssertFalse(model.creationLimitReached)
    }
}
