@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class UserProfileEndpointTests: XCTestCase {
    func testProfileCollectionEndpointsCarryScopeAndCursor() {
        let posts = Endpoint.userPosts(userId: "user/id", postTypes: "review", after: "post-cursor")
        XCTAssertEqual(posts.path, "/api/v1/posts")
        XCTAssertEqual(query(posts), [
            "after": "post-cursor", "creator": "user/id", "limit": "25",
            "post_types": "review", "sort": "new"
        ])

        let topics = Endpoint.userTopicsFollowing(userId: "user/id", after: "topic-cursor")
        XCTAssertEqual(topics.path, "/api/v1/users/user%2Fid/topics/following")
        XCTAssertEqual(query(topics), ["after": "topic-cursor", "limit": "25"])

        let communities = Endpoint.userCommunitiesMember(userId: "user/id", after: "community-cursor")
        XCTAssertEqual(communities.path, "/api/v1/users/user%2Fid/communities/member")
        XCTAssertEqual(query(communities), ["after": "community-cursor", "limit": "25"])

        let sources = Endpoint.userRssFeeds(
            userId: "user/id", feedType: "article", after: "source-cursor", limit: 25
        )
        XCTAssertEqual(query(sources), ["after": "source-cursor", "feed_type": "article", "limit": "25"])
    }

    func testProfileFixtureDecodesHeaderMetricsAndLinks() throws {
        let response = try makeVouchaDecoder().decode(
            UserProfileResponse.self,
            from: ApiFixtureLoader.data("native.users.profile.default")
        )
        XCTAssertEqual(response.user.id, "user-abc")
        XCTAssertEqual(response.user.username, "alice")
        XCTAssertEqual(response.userMetrics?.visibleCount("reviews"), 4)
        XCTAssertEqual(response.userMetrics?.visibleCount("communities_member"), 9)
        XCTAssertEqual(response.profileLinks.first?.url, "https://example.test")
    }

    func testRestrictedProfileFixtureDoesNotExposeLegacyCollectionAggregates() throws {
        let data = ApiFixtureLoader.data("native.users.profile.restricted")
        let response = try makeVouchaDecoder().decode(UserProfileResponse.self, from: data)
        XCTAssertEqual(response.userMetrics?.countCapped?["comments"], false)

        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
        XCTAssertNil(metrics["bookmarks"])
        XCTAssertNil(metrics["bookmarkers"])
        XCTAssertNil(metrics["bookmarks__updated_at"])
    }

    func testPrivateCountsDecodeWithTheirMatchingCapFlags() throws {
        let metrics = try makeVouchaDecoder().decode(UserProfileMetrics.self, from: Data(#"""
        {"id":"user-1","count":{"users_followers":1000},
        "count_capped":{"users_followers":true},
        "private_count":{"posts_saved":1000,"posts_hidden":3},
        "private_count_capped":{"posts_saved":true,"posts_hidden":false}}
        """#.utf8))

        XCTAssertEqual(
            try Set(XCTUnwrap(metrics.privateCount).keys),
            try Set(XCTUnwrap(metrics.privateCountCapped).keys)
        )
        XCTAssertEqual(metrics.privateCount?["posts_saved"], 1_000)
        XCTAssertEqual(metrics.privateCountCapped?["posts_saved"], true)
        XCTAssertEqual(metrics.privateCount?["posts_hidden"], 3)
        XCTAssertEqual(metrics.privateCountCapped?["posts_hidden"], false)
        XCTAssertTrue(metrics.isVisibleCountCapped("users_followers"))
        XCTAssertEqual(metrics.visibleCount("unknown"), 0)
        XCTAssertFalse(metrics.isVisibleCountCapped("unknown"))
    }

    func testAbsentCapMapsKeepCountsExact() throws {
        let metrics = try makeVouchaDecoder().decode(UserProfileMetrics.self, from: Data(#"""
        {"id":"user-1","count":{"reviews":4},"private_count":{"posts_saved":1000}}
        """#.utf8))
        XCTAssertEqual(metrics.visibleCount("reviews"), 4)
        XCTAssertFalse(metrics.isVisibleCountCapped("reviews"))
        XCTAssertFalse(metrics.isVisibleCountCapped("posts_saved"))
    }

    func testExactViewerCountsOverridePublicCapsIncludingZero() throws {
        let metrics = try makeVouchaDecoder().decode(UserProfileMetrics.self, from: Data(#"""
        {"id":"user-1","count":{"reviews":1000,"comments":1000,"discussions":1000},
        "viewer_count":{"reviews":7,"comments":0},
        "count_capped":{"reviews":true,"comments":true,"discussions":true}}
        """#.utf8))
        XCTAssertEqual(metrics.visibleCount("reviews"), 7)
        XCTAssertFalse(metrics.isVisibleCountCapped("reviews"))
        XCTAssertEqual(metrics.visibleCount("comments"), 0)
        XCTAssertFalse(metrics.isVisibleCountCapped("comments"))
        XCTAssertTrue(metrics.isVisibleCountCapped("discussions"))
    }

    private func query(_ endpoint: Endpoint) -> [String: String] {
        Dictionary(uniqueKeysWithValues: endpoint.queryItems.compactMap { item in
            item.value.map { (item.name, $0) }
        })
    }
}
