import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class CommunityAgentPromptPaginationTests: XCTestCase {
    func testPromptPageDecodesGlobalSlotsAndRoundTripsRequiredNullableCreator() throws {
        var object = try fixture()
        var prompts = try XCTUnwrap(object["community_agent_prompts"] as? [[String: Any]])
        prompts[0]["created_by_id"] = NSNull()
        object["community_agent_prompts"] = prompts
        let response = try decode(object)
        XCTAssertNil(response.communityAgentPrompts.first?.createdById)
        XCTAssertFalse(response.pageInfo.hasNextPage)
        XCTAssertNil(response.pageInfo.endCursor)
        XCTAssertEqual(response.slotInfo.used, 1)
        XCTAssertEqual(response.slotInfo.limitsByPlan.plus, 3)
        XCTAssertEqual(response.slotInfo.limitsByPlan.pro, 10)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(response)) as? [String: Any])
        let encodedPrompts = try XCTUnwrap(encoded["community_agent_prompts"] as? [[String: Any]])
        XCTAssertTrue(encodedPrompts[0]["created_by_id"] is NSNull)
        XCTAssertTrue(encodedPrompts[0]["deleted_at"] is NSNull)
        try assertFixtureCoversDTO("web.communities.agent-prompts.default", as: CommunityAgentPromptsResponse.self)
    }

    func testPromptPageRejectsMissingRequiredEnvelopeAndNullableFields() throws {
        for key in ["community_agent_prompts", "page_info", "slot_info"] {
            var object = try fixture()
            object.removeValue(forKey: key)
            XCTAssertThrowsError(try decode(object), key)
        }
        for key in ["start_cursor", "end_cursor", "has_next_page"] {
            var object = try fixture()
            var info = try XCTUnwrap(object["page_info"] as? [String: Any])
            info.removeValue(forKey: key)
            object["page_info"] = info
            XCTAssertThrowsError(try decode(object), key)
        }
        for key in ["created_by_id", "activated_at", "deactivated_at", "deleted_at", "deleted_by_id"] {
            var object = try fixture()
            var prompts = try XCTUnwrap(object["community_agent_prompts"] as? [[String: Any]])
            prompts[0].removeValue(forKey: key)
            object["community_agent_prompts"] = prompts
            XCTAssertThrowsError(try decode(object), key)
        }
        for key in ["used", "limit", "remaining", "limits_by_plan"] {
            var object = try fixture()
            var info = try XCTUnwrap(object["slot_info"] as? [String: Any])
            info.removeValue(forKey: key)
            object["slot_info"] = info
            XCTAssertThrowsError(try decode(object), key)
        }
    }

    func testPromptPageEndpointForwardsOpaqueCursorAndLeavesHistoryBeforeUnchanged() {
        let endpoint = Endpoint.communityAgentPrompts(idOrSlug: "a/b", after: "opaque+/=", limit: 7)
        XCTAssertEqual(endpoint.path, "/api/v1/communities/a%2Fb/agent-prompts")
        XCTAssertEqual(endpoint.queryItems.map(\.name), ["limit", "after"])
        XCTAssertEqual(endpoint.queryItems.map(\.value), ["7", "opaque+/="])
        let history = Endpoint.communityAgentPromptHistory(idOrSlug: "builders", before: "prompt-9")
        XCTAssertTrue(history.queryItems.contains { $0.name == "before" && $0.value == "prompt-9" })
        XCTAssertFalse(history.queryItems.contains { $0.name == "after" })
    }

    private func fixture() throws -> [String: Any] {
        try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.agent-prompts.default")
        ) as? [String: Any])
    }

    private func decode(_ object: [String: Any]) throws -> CommunityAgentPromptsResponse {
        try makeVouchaDecoder().decode(
            CommunityAgentPromptsResponse.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
    }
}
