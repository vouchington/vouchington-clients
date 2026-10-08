import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class CommunityModerationWorkloadDecodingTests: XCTestCase {
    func testCommunityAnalyticsDecodesNonemptyModeratorWorkload() throws {
        var payload = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("web.communities.moderation-analytics.default")
        ) as? [String: Any])
        payload["moderator_workload"] = [
            "moderators": [[
                "actor_user_id": "moderator-1",
                "total": 3,
                "counts": ["approve": 3],
                "weekly_counts": [["date": "2026-01-01", "type": "approve", "count": 3]]
            ]],
            "users": ["moderator-1": ["username": "Moderator"]]
        ]

        let analytics = try JSONDecoder.vouchaFixtureDecoder.decode(
            CommunityModerationAnalytics.self,
            from: JSONSerialization.data(withJSONObject: payload)
        )
        let moderator = try XCTUnwrap(analytics.moderatorWorkload.moderators.first)
        XCTAssertEqual(moderator.actorId, "moderator-1")
        XCTAssertEqual(moderator.total, 3)
        XCTAssertEqual(moderator.counts["approve"], 3)
        XCTAssertEqual(moderator.weeklyCounts.first?.count, 3)

        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(
            with: encoder.encode(moderator)
        ) as? [String: Any])
        XCTAssertEqual(encoded["actor_user_id"] as? String, "moderator-1")
        XCTAssertNil(encoded["actor_id"])
    }
}
