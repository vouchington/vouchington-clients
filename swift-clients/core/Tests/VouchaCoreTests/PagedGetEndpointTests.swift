import VouchaAPI
import XCTest

final class PagedGetEndpointTests: XCTestCase {
    func testCursorPagesPutLimitFirstAndOmitAMissingCursor() {
        let pages = [
            Endpoint.paymentCards(limit: 2),
            Endpoint.myEmailAddresses(limit: 2),
            Endpoint.authSessions(limit: 2),
            Endpoint.myApiKeys(limit: 2),
            Endpoint.myPushSubscriptions(limit: 2),
            Endpoint.myOAuthGrants(limit: 2),
            Endpoint.friendRecommendations(limit: 2),
            Endpoint.pointValuations(limit: 2),
            Endpoint.rewardsProgramStatuses(limit: 2),
            Endpoint.spendingCategories(limit: 2),
            Endpoint.userTopicsFollowing(userId: "user/1", limit: 2),
            Endpoint.userTopics(userId: "user/1", listType: "following", limit: 2)
        ]

        for page in pages {
            XCTAssertEqual(page.method, .GET)
            XCTAssertEqual(page.queryItems.map(\.name), ["limit"])
            XCTAssertEqual(page.queryItems.map(\.value), ["2"])
        }
        XCTAssertEqual(
            Endpoint.userTopicsFollowing(userId: "user/1", limit: 2).path,
            "/api/v1/users/user%2F1/topics/following"
        )
    }

    func testCursorPagesKeepAnOpaqueCursorAfterLimit() {
        let cursor = "opaque+/=cursor"
        let pages = [
            Endpoint.paymentCards(after: cursor, limit: 10),
            Endpoint.myEmailAddresses(after: cursor, limit: 10),
            Endpoint.authSessions(after: cursor, limit: 10),
            Endpoint.myApiKeys(after: cursor, limit: 10),
            Endpoint.myPushSubscriptions(after: cursor, limit: 10),
            Endpoint.myOAuthGrants(after: cursor, limit: 10),
            Endpoint.friendRecommendations(after: cursor, limit: 10),
            Endpoint.pointValuations(after: cursor, limit: 10),
            Endpoint.rewardsProgramStatuses(after: cursor, limit: 10),
            Endpoint.spendingCategories(after: cursor, limit: 10),
            Endpoint.adminAiCosts(after: cursor),
            Endpoint.userTopicsFollowing(userId: "ada", after: cursor, limit: 10),
            Endpoint.userPosts(
                userId: "ada",
                listType: "bookmarks",
                mediaType: "video",
                after: cursor,
                limit: 10
            )
        ]

        for page in pages {
            XCTAssertEqual(Array(page.queryItems.map(\.name).prefix(2)), ["limit", "after"])
            XCTAssertEqual(page.queryItems.first { $0.name == "after" }?.value, cursor)
        }
        XCTAssertEqual(Endpoint.adminAiCosts(after: cursor).queryItems.map(\.value), ["25", cursor])
        let posts = Endpoint.userPosts(
            userId: "ada",
            listType: "bookmarks",
            mediaType: "video",
            after: cursor,
            limit: 10
        )
        XCTAssertEqual(posts.queryItems.map(\.name), ["limit", "after", "media_type"])
        XCTAssertEqual(posts.queryItems.map(\.value), ["10", cursor, "video"])
        XCTAssertEqual(posts.path, "/api/v1/users/ada/posts/bookmarks")
    }

    func testOptionalCursorPagesOmitNilLimitAndKeepLaterFilters() {
        XCTAssertTrue(Endpoint.currencies().queryItems.isEmpty)
        XCTAssertEqual(Endpoint.currencies(after: "cursor").queryItems, [
            URLQueryItem(name: "after", value: "cursor")
        ])
        XCTAssertEqual(Endpoint.copyrightNotices(limit: 40).queryItems, [
            URLQueryItem(name: "limit", value: "40")
        ])
        XCTAssertEqual(Endpoint.postAncestors(postId: "comment 1").queryItems, [])
        XCTAssertEqual(
            Endpoint.householdMemberships(householdId: "house/1").path,
            "/api/v1/households/house%2F1/memberships"
        )
        XCTAssertTrue(Endpoint.householdMemberships(householdId: "house/1").queryItems.isEmpty)
        XCTAssertEqual(
            Endpoint.myConversations(after: "cursor-5").queryItems,
            [URLQueryItem(name: "after", value: "cursor-5")]
        )
        XCTAssertEqual(
            Endpoint.fediverseInstances(limit: nil, query: "social", sort: nil).queryItems,
            [URLQueryItem(name: "q", value: "social")]
        )
        XCTAssertEqual(
            Endpoint.fediverseInstances().queryItems,
            [
                URLQueryItem(name: "limit", value: "25"),
                URLQueryItem(name: "sort", value: "best")
            ]
        )
    }
}
