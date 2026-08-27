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
        XCTAssertEqual(response.userMetrics?.visibleCount("reviews"), 1)
        XCTAssertEqual(response.userMetrics?.visibleCount("communities_member"), 9)
        XCTAssertEqual(response.profileLinks.first?.url, "https://example.test")
    }

    func testRestrictedProfileFixtureDoesNotExposeLegacyCollectionAggregates() throws {
        let data = ApiFixtureLoader.data("native.users.profile.restricted")
        let response = try makeVouchaDecoder().decode(UserProfileResponse.self, from: data)
        XCTAssertEqual(response.userMetrics?.bookmarkers?["follow"], 0)

        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let metrics = try XCTUnwrap(root["user_metrics"] as? [String: Any])
        let bookmarks = try XCTUnwrap(metrics["bookmarks"] as? [String: Any])
        let follow = try XCTUnwrap(bookmarks["follow"] as? [String: Int])
        XCTAssertEqual(follow, ["topics": 0, "posts": 0, "users": 0])
    }

    private func query(_ endpoint: Endpoint) -> [String: String] {
        Dictionary(uniqueKeysWithValues: endpoint.queryItems.compactMap { item in
            item.value.map { (item.name, $0) }
        })
    }
}
