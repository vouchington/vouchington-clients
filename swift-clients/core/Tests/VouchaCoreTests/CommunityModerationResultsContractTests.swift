import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CommunityModerationResultsContractTests: XCTestCase {
    func testFixtureDecodesEmptyPageAndKeepsPlatformStatus() throws {
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            CommunityModerationResultsResponse.self,
            from: ApiFixtureLoader.data("web.communities.moderation-results.default")
        )

        XCTAssertTrue(response.results.isEmpty)
        XCTAssertFalse(response.pageInfo.hasNextPage)
        XCTAssertNil(response.pageInfo.endCursor)
        XCTAssertEqual(response.platformModeration.status, .inReview)
    }

    func testSummaryRowsDecodeWithoutRawResultPayload() throws {
        var payload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.moderation-results.default")
        ) as? [String: Any])
        payload["results"] = [[
            "id": "moderation-1",
            "post_id": "post-1",
            "community_prompt_id": "prompt-1",
            "is_flagged": true,
            "created_at": "2026-01-02T03:04:05.000Z"
        ]]
        payload["page_info"] = ["end_cursor": "cursor-1", "has_next_page": true, "start_cursor": "cursor-0"]

        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            CommunityModerationResultsResponse.self,
            from: JSONSerialization.data(withJSONObject: payload)
        )

        let row = try XCTUnwrap(response.results.first)
        XCTAssertEqual(row.id, "moderation-1")
        XCTAssertEqual(row.postId, "post-1")
        XCTAssertEqual(row.communityPromptId, "prompt-1")
        XCTAssertTrue(row.isFlagged)
        XCTAssertTrue(response.pageInfo.hasNextPage)
        XCTAssertEqual(response.pageInfo.endCursor, "cursor-1")
    }

    func testEndpointOmitsPagingQueryByDefault() {
        let endpoint = Endpoint.communityModerationResults(idOrSlug: "builders", postId: "post 1")

        XCTAssertEqual(endpoint.path, "/api/v1/communities/builders/posts/post%201/moderation-results")
        XCTAssertTrue(endpoint.queryItems.isEmpty)
    }

    func testEndpointForwardsOpaqueCursorAndLimit() {
        let endpoint = Endpoint.communityModerationResults(
            idOrSlug: "builders",
            postId: "post-1",
            after: "opaque+/=",
            limit: 100
        )

        XCTAssertEqual(endpoint.queryItems.map(\.name), ["limit", "after"])
        XCTAssertEqual(endpoint.queryItems.map(\.value), ["100", "opaque+/="])
    }
}
