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

    func testSuccessfulCreationRefreshesListAndClearsSubmittedDraftWithoutInferringCreationCap() async throws {
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
        let parent = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation,
            currentUserId: "user-1",
            isAdministrator: true
        )
        let model = parent.agentPromptWorkspace
        model.draftPrompt = "Keep draft"
        await model.reload()
        await parent.createCommunityAgentPrompt(prompt: model.draftPrompt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET", "POST", "GET"])
        XCTAssertEqual(model.slotInfo?.used, 9)
        XCTAssertEqual(model.slotInfo?.remaining, 1)
        XCTAssertEqual(model.pagination.items.map(\.id), ["created-prompt"])
        XCTAssertEqual(model.pagination.items.first?.prompt, "Newly created prompt")
        XCTAssertEqual(model.draftPrompt, "")
        XCTAssertNotNil(parent.statusMessage)
        XCTAssertFalse(model.creationLimitReached)
    }

    func testConfirmedPostClearsWhitespaceDraftEvenWhenRefreshFailsAndRetainsPage() async throws {
        let fixture = ApiFixtureLoader.data("web.communities.agent-prompts.default")
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (fixture, 200, 0),
            (Data("{}".utf8), 200, 0),
            (Data("{}".utf8), 503, 0)
        ]
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        model.selectPrompt("prompt-1")
        let slots = model.slotInfo
        model.draftPrompt = "  Submitted draft \n"
        let created = await model.create(prompt: model.draftPrompt)
        XCTAssertTrue(created)
        XCTAssertEqual(model.draftPrompt, "")
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
        XCTAssertEqual(model.selectedPromptId, "prompt-1")
        XCTAssertEqual(model.slotInfo?.used, slots?.used)
        XCTAssertNotNil(model.refreshError)
    }

    func testEditedDraftSurvivesConfirmedPost() async throws {
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("web.communities.agent-prompts.default"), 200)
        CannedFeedURLProtocol.handlers["POST " + path] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        model.draftPrompt = "Submitted"
        let create = Task { await model.create(prompt: model.draftPrompt) }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "POST").wait()
        model.draftPrompt = "Edited while pending"
        CannedFeedURLProtocol.releaseResponse(path: path)
        let created = await create.value
        XCTAssertTrue(created)
        XCTAssertEqual(model.draftPrompt, "Edited while pending")
    }

    func testCancelledRefreshRetainsSelectedPageAndOpaqueContinuation() async throws {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
        object["page_info"] = ["has_next_page": true, "start_cursor": NSNull(), "end_cursor": "retained-opaque"]
        CannedFeedURLProtocol.handlers[path] = try (JSONSerialization.data(withJSONObject: object), 200)
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        await model.reload()
        model.selectPrompt("prompt-1")
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let refresh = Task { await model.reload() }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "GET").wait()
        XCTAssertEqual(model.selectedPromptId, "prompt-1")
        refresh.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await refresh.value
        XCTAssertEqual(model.pagination.items.map(\.id), ["prompt-1"])
        XCTAssertEqual(model.selectedPromptId, "prompt-1")
        XCTAssertTrue(model.pagination.hasMore)
        XCTAssertEqual(model.slotInfo?.limit, 3)
        XCTAssertNil(model.refreshError)
        await model.loadMore()
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.last?.query?.contains("after=retained-opaque") == true)
    }

    func testConfirmedCreationAfterDisappearanceClearsOnlyOriginalAccountDraft() async throws {
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let model = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        let otherAccount = try CommunityAgentPromptWorkspace(client: makeClient(), slug: "builders")
        model.draftPrompt = "  Submitted  "
        otherAccount.draftPrompt = "Other account draft"
        let create = Task { await model.create(prompt: model.draftPrompt) }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "POST").wait()
        model.invalidate()
        CannedFeedURLProtocol.releaseResponse(path: path)
        let current = await create.value
        XCTAssertFalse(current)
        XCTAssertEqual(model.draftPrompt, "")
        XCTAssertEqual(otherAccount.draftPrompt, "Other account draft")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
    }

    func testConfirmedDeletionRemovesTargetBeforeFailedRefreshWithoutChangingSlots() async throws {
        var page = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
        var prompts = try XCTUnwrap(page["community_agent_prompts"] as? [[String: Any]])
        var other = prompts[0]
        other["id"] = "retained-prompt"
        prompts.append(other)
        page["community_agent_prompts"] = prompts
        page["page_info"] = ["has_next_page": true, "start_cursor": NSNull(), "end_cursor": "retained-cursor"]
        CannedFeedURLProtocol.handlers[path] = try (JSONSerialization.data(withJSONObject: page), 200)
        let parent = try CommunityDetailViewModel(
            client: makeClient(), slug: "builders", initialTab: .moderation,
            currentUserId: "user-1", isAdministrator: true
        )
        let model = parent.agentPromptWorkspace
        await model.reload()
        model.selectPrompt("prompt-1")
        let used = model.slotInfo?.used
        CannedFeedURLProtocol.handlers["DELETE " + path + "/prompt-1"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 503)
        await parent.deleteCommunityAgentPrompt(promptId: "prompt-1")
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.contains {
            $0.method == "DELETE" && $0.url.path == path + "/prompt-1"
        })
        XCTAssertEqual(model.pagination.items.map(\.id), ["retained-prompt"])
        XCTAssertTrue(model.pagination.hasMore)
        XCTAssertEqual(model.pagination.endCursor, "retained-cursor")
        XCTAssertNil(model.selectedPromptId)
        XCTAssertFalse(parent.canDeleteAgentPrompt("prompt-1"))
        XCTAssertEqual(model.slotInfo?.used, used)
        XCTAssertNotNil(model.refreshError)
        let revision = model.revision
        model.acknowledgeDeletion("retained-prompt", ifCurrent: revision - 1)
        XCTAssertEqual(model.pagination.items.map(\.id), ["retained-prompt"])
    }

}
