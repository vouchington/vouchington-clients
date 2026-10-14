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
            Endpoint.userTopics(userId: "user/1", listType: "following", limit: 2),
            Endpoint.trendingCommunities(limit: 2),
            Endpoint.trendingReferralPrograms(limit: 2),
            Endpoint.referralLinksFeed(feedType: "follow_users", limit: 2),
            Endpoint.topicRecommendations(limit: 2),
            Endpoint.myReferralClicks(limit: 2),
            Endpoint.referralLinks(limit: 2),
            Endpoint.myConversationMessages(conversationId: "conv/1", limit: 2),
            Endpoint.myMessages(limit: 2),
            Endpoint.myMessageConversationMessages(conversationId: "conv/1", limit: 2)
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
            ),
            Endpoint.trendingCommunities(after: cursor, limit: 10),
            Endpoint.trendingReferralPrograms(after: cursor, limit: 10),
            Endpoint.referralLinksFeed(feedType: "follow_users", after: cursor, limit: 10),
            Endpoint.topicRecommendations(after: cursor, limit: 10),
            Endpoint.myReferralClicks(after: cursor, limit: 10),
            Endpoint.referralLinks(after: cursor, limit: 10),
            Endpoint.myConversationMessages(conversationId: "conv/1", after: cursor, limit: 10),
            Endpoint.myMessages(after: cursor, limit: 10),
            Endpoint.myMessageConversationMessages(conversationId: "conv/1", after: cursor, limit: 10)
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

    func testRemainingLimitThenAfterPagesOmitAMissingCursor() {
        let pages = [
            Endpoint.notifications(limit: 2),
            Endpoint.userFollowing(userId: "user/1", limit: 2),
            Endpoint.lists(limit: 2),
            Endpoint.rssFeedCrawls(id: "feed/1", limit: 2),
            Endpoint.adminReviewQueue(limit: 2),
            Endpoint.urlCrawls(urlId: "url/1", limit: 2),
            Endpoint.communityPendingPosts(idOrSlug: "comm/1", limit: 2),
            Endpoint.communityInvites(idOrSlug: "comm/1", limit: 2),
            Endpoint.communityListItems(idOrSlug: "comm/1", itemType: .topic, limit: 2),
            Endpoint.rssFeedItems(feedType: "any", limit: 2),
            Endpoint.allRssFeedItems(limit: 2),
            Endpoint.allRssFeeds(limit: 2),
            Endpoint.userRssFeeds(userId: "user/1", listType: "muted", limit: 2)
        ]

        for page in pages {
            XCTAssertEqual(page.method, .GET)
            XCTAssertEqual(page.queryItems.map(\.name), ["limit"])
            XCTAssertEqual(page.queryItems.map(\.value), ["2"])
        }
        XCTAssertEqual(
            Endpoint.userFollowing(userId: "user/1", limit: 2).path,
            "/api/v1/users/user%2F1/users/following"
        )
        XCTAssertEqual(Endpoint.rssFeedCrawls(id: "feed/1", limit: 2).path, "/api/v1/rss-feeds/feed%2F1/crawls")
        XCTAssertEqual(Endpoint.urlCrawls(urlId: "url/1", limit: 2).path, "/api/v1/urls/url%2F1/crawls")
        XCTAssertEqual(
            Endpoint.communityPendingPosts(idOrSlug: "comm/1", limit: 2).path,
            "/api/v1/communities/comm%2F1/posts/pending"
        )
        XCTAssertEqual(
            Endpoint.communityInvites(idOrSlug: "comm/1", limit: 2).path,
            "/api/v1/communities/comm%2F1/invites"
        )
        XCTAssertEqual(
            Endpoint.communityListItems(idOrSlug: "comm/1", itemType: .topic, limit: 2).path,
            "/api/v1/communities/comm%2F1/list-items/topics"
        )
        XCTAssertEqual(
            Endpoint.userRssFeeds(userId: "user/1", listType: "muted", limit: 2).path,
            "/api/v1/users/user%2F1/rss-feeds/muted"
        )
        XCTAssertEqual(Endpoint.notifications(limit: 2).path, "/api/v1/my/notifications")
        XCTAssertEqual(Endpoint.adminReviewQueue(limit: 2).path, "/api/v1/posts/review-queue")
        XCTAssertEqual(Endpoint.lists(limit: 2).path, "/api/v1/lists")
        XCTAssertEqual(Endpoint.rssFeedItems(feedType: "any", limit: 2).path, "/api/v1/feeds/rss_feed_items/any")
        XCTAssertEqual(Endpoint.allRssFeedItems(limit: 2).path, "/api/v1/rss-feed-items")
        XCTAssertEqual(Endpoint.allRssFeeds(limit: 2).path, "/api/v1/rss-feeds")
    }

    func testRemainingLimitThenAfterPagesKeepCursorAndLaterFilters() {
        let cursor = "opaque+/=cursor"
        let pages = [
            Endpoint.notifications(after: cursor, limit: 10),
            Endpoint.userFollowing(userId: "ada", after: cursor, limit: 10),
            Endpoint.lists(after: cursor, limit: 10),
            Endpoint.rssFeedCrawls(id: "feed 1", after: cursor, limit: 10),
            Endpoint.adminReviewQueue(after: cursor, limit: 10),
            Endpoint.urlCrawls(urlId: "url 1", after: cursor, limit: 10),
            Endpoint.communityPendingPosts(idOrSlug: "test community", after: cursor, limit: 10),
            Endpoint.communityInvites(idOrSlug: "test community", after: cursor, limit: 10),
            Endpoint.communityListItems(idOrSlug: "test community", itemType: .topic, after: cursor, limit: 10),
            Endpoint.rssFeedItems(feedType: "any", after: cursor, limit: 10, mediaType: "audio"),
            Endpoint.allRssFeedItems(after: cursor, limit: 10, mediaType: "audio"),
            Endpoint.allRssFeeds(feedType: "podcast", after: cursor, limit: 10, category: "news"),
            Endpoint.userRssFeeds(userId: "user 1", listType: "muted", feedType: "podcast", after: cursor, limit: 10)
        ]

        for page in pages {
            XCTAssertEqual(Array(page.queryItems.map(\.name).prefix(2)), ["limit", "after"])
            XCTAssertEqual(page.queryItems.first { $0.name == "after" }?.value, cursor)
        }
        XCTAssertEqual(
            Endpoint.rssFeedItems(feedType: "any", after: cursor, limit: 10, mediaType: "audio").queryItems.map(\.name),
            ["limit", "after", "media_type"]
        )
        XCTAssertEqual(
            Endpoint.allRssFeedItems(after: cursor, limit: 10, mediaType: "audio").queryItems.map(\.value),
            ["10", cursor, "audio"]
        )
        XCTAssertEqual(
            Endpoint.allRssFeeds(feedType: "podcast", after: cursor, limit: 10, category: "news").queryItems,
            [
                URLQueryItem(name: "limit", value: "10"),
                URLQueryItem(name: "after", value: cursor),
                URLQueryItem(name: "feed_type", value: "podcast"),
                URLQueryItem(name: "category", value: "news")
            ]
        )
        XCTAssertEqual(
            Endpoint.userRssFeeds(userId: "user 1", feedType: "podcast", limit: 8).queryItems,
            [
                URLQueryItem(name: "limit", value: "8"),
                URLQueryItem(name: "feed_type", value: "podcast")
            ]
        )
    }
}
