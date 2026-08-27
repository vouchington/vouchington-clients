import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import XCTest

final class EndpointCoverageTests: XCTestCase {
    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
        super.tearDown()
    }

    func testAllRssFeedsEncodesCursorAndFeedType() async throws {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CapturingURLProtocol.self]
        )

        struct EmptyResponse: Decodable {}
        let _: EmptyResponse = try await client.send(
            Endpoint.allRssFeeds(feedType: "podcast", after: "cursor one?two", limit: 12, category: "business")
        )

        let url = try XCTUnwrap(CapturingURLProtocol.lastRequestURL)
        XCTAssertEqual(url.path, "/api/v1/rss-feeds")
        XCTAssertTrue(url.absoluteString.contains("limit=12"))

        let components = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false))
        XCTAssertEqual(components.queryItems?.first(where: { $0.name == "after" })?.value, "cursor one?two")
        XCTAssertEqual(components.queryItems?.first(where: { $0.name == "feed_type" })?.value, "podcast")
        XCTAssertEqual(components.queryItems?.first(where: { $0.name == "category" })?.value, "business")
    }

    func testRssFeedFollowHelpersUseBookmarkRoutes() {
        let follow = Endpoint.followRssFeed(rssFeedId: "feed-123")
        XCTAssertEqual(follow.method, .PUT)
        XCTAssertEqual(follow.path, "/api/v1/bookmarks/rss_feed/feed-123/follow")

        let unfollow = Endpoint.unfollowRssFeed(rssFeedId: "feed-123")
        XCTAssertEqual(unfollow.method, .DELETE)
        XCTAssertEqual(unfollow.path, "/api/v1/bookmarks/rss_feed/feed-123/follow")
    }

    func testAuthAndProfileEndpointsUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.requestEmailOTP(
                email: "test@example.com",
                turnstileToken: "captcha-token",
                uiLocale: "fr"
            ),
            method: .POST,
            path: "/api/v1/auth/email-address/tokens",
            body: [
                "email_address": "test@example.com",
                "cf_turnstile_response": "captcha-token",
                "ui_locale": "fr"
            ]
        )
        assertEndpoint(
            Endpoint.verifyEmailOTP(email: "test@example.com", code: "123456"),
            method: .POST,
            path: "/api/v1/auth/email-address/login",
            body: ["emailAddress": "test@example.com", "token": "123456"]
        )
        assertEndpoint(Endpoint.logout, method: .POST, path: "/api/v1/auth/logout")
        assertEndpoint(Endpoint.myIdentity, path: "/api/v1/my/identity")
        assertEndpoint(Endpoint.myProfile, path: "/api/v1/my/profile")
        assertEndpoint(
            Endpoint.markdownPreview(markdown: "**Hello**"),
            method: .POST,
            path: "/api/v1/markdown/preview",
            body: ["markdown": "**Hello**"]
        )
        assertEndpoint(
            Endpoint.updateProfile(markdown: "hello"),
            method: .PATCH,
            path: "/api/v1/my/profile",
            body: ["markdown": "hello"]
        )
    }

    func testFediverseSearchEndpointEncodesOptionalFilters() {
        let endpoint = Endpoint.fediverseSearch(
            query: "swift",
            providers: ["peertube", "mastodon"],
            type: "video",
            limit: 12,
            after: "cursor-1"
        )

        assertEndpoint(endpoint, path: "/api/v1/fediverse/search")
        XCTAssertEqual(
            endpoint.queryItems,
            [
                URLQueryItem(name: "q", value: "swift"),
                URLQueryItem(name: "providers", value: "peertube,mastodon"),
                URLQueryItem(name: "type", value: "video"),
                URLQueryItem(name: "limit", value: "12"),
                URLQueryItem(name: "after", value: "cursor-1")
            ]
        )
    }

    func testFeatureFlagsEndpointUsesPublicRoute() {
        assertEndpoint(Endpoint.featureFlags, path: "/api/v1/feature-flags")
    }

    func testCaptchaConfigEndpointUsesPublicRoute() {
        assertEndpoint(Endpoint.captchaConfig, path: "/api/v1/captcha-config")
    }

    func testAppleSignInAndMFAEndpointsEncodeBodies() {
        assertEndpoint(
            Endpoint.appleSignIn(token: "token", nonce: "nonce", userName: "Pat"),
            method: .POST,
            path: "/api/v1/auth/oauth/apple/continue",
            body: ["nonce": "nonce", "token": "token", "userData": ["name": "Pat"]]
        )
        assertEndpoint(
            Endpoint.verifyMFATOTP(loginAttemptId: "attempt-1", code: "654321"),
            method: .POST,
            path: "/api/v1/auth/mfa/totp/verification",
            body: ["login_attempt_id": "attempt-1", "code": "654321"]
        )
    }

    func testVoteAndUserFollowEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.post(idOrSlug: "post 1"), path: "/api/v1/posts/post%201")
        assertEndpoint(Endpoint.postDescendants(postId: "post 1"), path: "/api/v1/posts/post%201/descendants")
        assertEndpoint(Endpoint.postAncestors(postId: "comment 1"), path: "/api/v1/posts/comment%201/ancestors")
        assertEndpoint(Endpoint.lockPost(postId: "post 1"), method: .POST, path: "/api/v1/posts/post%201/lock")
        assertEndpoint(Endpoint.unlockPost(postId: "post 1"), method: .DELETE, path: "/api/v1/posts/post%201/lock")
        assertEndpoint(
            Endpoint.votePost(postId: "post-1", choice: .dislike),
            method: .PUT,
            path: "/api/v1/posts/post-1/vote",
            body: ["choice": "dislike"]
        )
        assertEndpoint(
            Endpoint.voteRssFeedItem(rssFeedItemId: "item-1", choice: .like),
            method: .PUT,
            path: "/api/v1/rss-feed-items/item-1/vote",
            body: ["choice": "like"]
        )
        assertEndpoint(
            Endpoint.voteTopic(topicId: "topic-1", choice: .neutral),
            method: .PUT,
            path: "/api/v1/topics/topic-1/vote",
            body: ["choice": "neutral"]
        )
        assertEndpoint(
            Endpoint.followUser(userId: "user 1"),
            method: .PUT,
            path: "/api/v1/bookmarks/user/user%201/follow"
        )
        assertEndpoint(
            Endpoint.unfollowUser(userId: "user 1"),
            method: .DELETE,
            path: "/api/v1/bookmarks/user/user%201/follow"
        )
    }

    func testAPIClientKeepsEncodedPathSegmentsSingleEncodedInRequestURL() async throws {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CapturingURLProtocol.self]
        )

        struct EmptyResponse: Decodable {}

        let _: EmptyResponse = try await client.send(Endpoint.topic(id: "topic 1"))
        XCTAssertEqual(
            CapturingURLProtocol.lastRequestURL?.absoluteString,
            "http://localhost:2999/api/v1/topics/topic%201"
        )

        let _: EmptyResponse = try await client.send(Endpoint.deletePost(postId: "post 1"))
        XCTAssertEqual(
            CapturingURLProtocol.lastRequestURL?.absoluteString,
            "http://localhost:2999/api/v1/posts/post%201"
        )

        let _: EmptyResponse = try await client.send(Endpoint.userFollowing(userId: "user 1"))
        XCTAssertEqual(
            CapturingURLProtocol.lastRequestURL?.absoluteString,
            "http://localhost:2999/api/v1/users/user%201/users/following?limit=100"
        )

        let _: EmptyResponse = try await client.send(Endpoint.userFollowers(userId: "user 2", after: "cursor one"))
        XCTAssertEqual(
            CapturingURLProtocol.lastRequestURL?.absoluteString,
            "http://localhost:2999/api/v1/users/user%202/users/followers?limit=100&after=cursor%20one"
        )
    }

    func testNotificationEndpointsUseExpectedRoutes() {
        let notifications = Endpoint.notifications(after: "cursor-1", limit: 9)
        XCTAssertEqual(notifications.path, "/api/v1/my/notifications")
        XCTAssertEqual(notifications.queryItems, [
            URLQueryItem(name: "limit", value: "9"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])

        assertEndpoint(
            Endpoint.markNotificationRead(notificationId: "note-1"),
            method: .PATCH,
            path: "/api/v1/my/notifications/note-1"
        )
        assertEndpoint(
            Endpoint.notificationRedirectTarget(notificationId: "note 1"),
            path: "/api/v1/my/notifications/note%201/redirect-target"
        )
        assertEndpoint(Endpoint.markAllNotificationsRead, method: .POST, path: "/api/v1/my/notifications/read-all")
    }

    func testGlobalFeedAndSourceEndpointsUseExpectedRoutes() {
        let items = Endpoint.allRssFeedItems(after: "cursor-2", limit: 15, mediaType: "audio")
        XCTAssertEqual(items.path, "/api/v1/rss-feed-items")
        XCTAssertEqual(items.queryItems, [
            URLQueryItem(name: "limit", value: "15"),
            URLQueryItem(name: "after", value: "cursor-2"),
            URLQueryItem(name: "media_type", value: "audio")
        ])

        let sources = Endpoint.userRssFeeds(userId: "user-1", listType: "muted", limit: 8)
        XCTAssertEqual(sources.path, "/api/v1/users/user-1/rss-feeds/muted")
        XCTAssertEqual(sources.queryItems, [URLQueryItem(name: "limit", value: "8")])

        let filteredSources = Endpoint.userRssFeeds(userId: "user-1", feedType: "podcast", limit: 8)
        XCTAssertEqual(filteredSources.path, "/api/v1/users/user-1/rss-feeds/following")
        XCTAssertEqual(filteredSources.queryItems, [
            URLQueryItem(name: "limit", value: "8"),
            URLQueryItem(name: "feed_type", value: "podcast")
        ])

        let topicSources = Endpoint.rssFeeds(
            topicIdentifier: "topic-1",
            includeDescendants: true,
            enabled: true,
            discoverable: false
        )
        XCTAssertEqual(topicSources.path, "/api/v1/rss-feeds")
        XCTAssertEqual(topicSources.queryItems, [
            URLQueryItem(name: "topic", value: "topic-1"),
            URLQueryItem(name: "include_descendants", value: "true"),
            URLQueryItem(name: "enabled", value: "true"),
            URLQueryItem(name: "discoverable", value: "false")
        ])
        XCTAssertEqual(
            Endpoint.rssFeeds(topicIdentifier: "topic-1", enabled: .all).queryItems,
            [
                URLQueryItem(name: "topic", value: "topic-1"),
                URLQueryItem(name: "enabled", value: "null")
            ]
        )

        assertEndpoint(
            Endpoint.createSource(rssFeedURL: "https://example.com/feed.xml", follow: true),
            method: .POST,
            path: "/api/v1/rss-feeds",
            body: ["rss_feed_url": "https://example.com/feed.xml", "follow": true]
        )
        assertEndpoint(
            Endpoint.updateRssFeed(rssFeedId: "feed 1", body: ["enabled": true]),
            method: .PATCH,
            path: "/api/v1/rss-feeds/feed%201",
            body: ["enabled": true]
        )
        assertEndpoint(Endpoint.deleteRssFeed(rssFeedId: "feed 1"), method: .DELETE, path: "/api/v1/rss-feeds/feed%201")
        assertEndpoint(
            Endpoint.refreshRssFeed(rssFeedId: "feed 1", body: ["force": true]),
            method: .POST,
            path: "/api/v1/rss-feeds/feed%201/refreshes",
            body: ["force": true]
        )
        assertEndpoint(
            Endpoint.shareRssFeedItemWithFollowers(rssFeedItemId: "item 1"),
            method: .POST,
            path: "/api/v1/rss-feed-items/item%201/shares"
        )
        assertEndpoint(
            Endpoint.sendRssFeedItemToFollowers(rssFeedItemId: "item 1", request: .allFollowers),
            method: .POST,
            path: "/api/v1/rss-feed-items/item%201/sends",
            body: ["audience": "all_followers"]
        )
    }

    func testListAndReadEndpointsUseExpectedRoutes() {
        let lists = Endpoint.lists(after: "cursor 1", limit: 9)
        XCTAssertEqual(lists.path, "/api/v1/lists")
        XCTAssertEqual(lists.queryItems, [
            URLQueryItem(name: "limit", value: "9"),
            URLQueryItem(name: "after", value: "cursor 1")
        ])

        assertEndpoint(
            Endpoint.createList(name: "Reading", description: "Saved articles", visibility: "public"),
            method: .POST,
            path: "/api/v1/lists",
            body: ["name": "Reading", "description": "Saved articles", "visibility": "public"]
        )
        assertEndpoint(Endpoint.list(listId: "list 1"), path: "/api/v1/lists/list%201")
        assertEndpoint(
            Endpoint.updateList(listId: "list 1", name: "Watch", visibility: "unlisted"),
            method: .PATCH,
            path: "/api/v1/lists/list%201",
            body: ["name": "Watch", "visibility": "unlisted"]
        )
        assertEndpoint(
            Endpoint.updateList(listId: "list 1", description: .null),
            method: .PATCH,
            path: "/api/v1/lists/list%201",
            body: ["description": NSNull()]
        )
        assertEndpoint(Endpoint.deleteList(listId: "list 1"), method: .DELETE, path: "/api/v1/lists/list%201")

        let items = Endpoint.listItems(listId: "list 1", mediaType: "video", read: false, after: "cursor 2", limit: 10)
        XCTAssertEqual(items.path, "/api/v1/lists/list%201/items")
        XCTAssertEqual(items.queryItems, [
            URLQueryItem(name: "limit", value: "10"),
            URLQueryItem(name: "media_type", value: "video"),
            URLQueryItem(name: "read", value: "false"),
            URLQueryItem(name: "after", value: "cursor 2")
        ])

        assertEndpoint(
            Endpoint.addListRssFeedItem(listId: "list 1", rssFeedItemId: "item 1"),
            method: .POST,
            path: "/api/v1/lists/list%201/items/rss-feed-items",
            body: ["rss_feed_item_id": "item 1"]
        )
        assertEndpoint(
            Endpoint.removeListRssFeedItem(listId: "list 1", rssFeedItemId: "item 1"),
            method: .DELETE,
            path: "/api/v1/lists/list%201/items/rss-feed-items/item%201"
        )
        assertEndpoint(
            Endpoint.addListPost(listId: "list 1", postId: "post 1"),
            method: .POST,
            path: "/api/v1/lists/list%201/items/posts",
            body: ["post_id": "post 1"]
        )
        assertEndpoint(
            Endpoint.removeListPost(listId: "list 1", postId: "post 1"),
            method: .DELETE,
            path: "/api/v1/lists/list%201/items/posts/post%201"
        )

        let containing = Endpoint.listsContaining(itemType: "rss_feed_item", entityId: "item 1")
        XCTAssertEqual(containing.path, "/api/v1/lists/contains")
        XCTAssertEqual(containing.queryItems, [
            URLQueryItem(name: "item_type", value: "rss_feed_item"),
            URLQueryItem(name: "entity_id", value: "item 1")
        ])
        assertEndpoint(
            Endpoint.importCommunityList(listId: "list 1", communitySlug: "community 1"),
            method: .POST,
            path: "/api/v1/lists/list%201/import",
            body: ["community_slug": "community 1"]
        )
        assertEndpoint(
            Endpoint.markRssFeedItemRead(rssFeedItemId: "item 1"),
            method: .PUT,
            path: "/api/v1/rss-feed-items/item%201/read"
        )
        assertEndpoint(
            Endpoint.markRssFeedItemUnread(rssFeedItemId: "item 1"),
            method: .DELETE,
            path: "/api/v1/rss-feed-items/item%201/read"
        )
        assertEndpoint(Endpoint.markPostRead(postId: "post 1"), method: .PUT, path: "/api/v1/posts/post%201/read")
        assertEndpoint(
            Endpoint.markPostUnread(postId: "post 1"),
            method: .DELETE,
            path: "/api/v1/posts/post%201/read"
        )
    }

    func testTopicMutationEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.topic(id: "topic 1"), path: "/api/v1/topics/topic%201")
        assertEndpoint(
            Endpoint.updateTopic(id: "topic 1", body: UpdateTopicBody(name: "Updated")),
            method: .PATCH,
            path: "/api/v1/topics/topic%201",
            body: ["name": "Updated"]
        )
        assertEndpoint(
            Endpoint.updateTopic(
                id: "topic 1",
                body: UpdateTopicBody(hostname: .null, logoImageId: .null, heroImageId: .null)
            ),
            method: .PATCH,
            path: "/api/v1/topics/topic%201",
            body: ["hostname": NSNull(), "logo_image_id": NSNull(), "hero_image_id": NSNull()]
        )
        assertEndpoint(
            Endpoint.topicTypeAttributes(topicId: "topic 1", typeSlug: "spending category"),
            path: "/api/v1/topics/topic%201/spending%20category"
        )
        assertEndpoint(
            Endpoint.updateTopicTypeAttributes(
                topicId: "topic 1",
                typeSlug: "spending category",
                body: ["category": "travel"]
            ),
            method: .PATCH,
            path: "/api/v1/topics/topic%201/spending%20category",
            body: ["category": "travel"]
        )
        assertEndpoint(Endpoint.topicAliases(topicId: "topic 1"), path: "/api/v1/topics/topic%201/aliases")
        assertEndpoint(
            Endpoint.createTopicAliases(topicId: "topic 1", aliases: "alias", overwrite: true),
            method: .POST,
            path: "/api/v1/topics/topic%201/aliases",
            body: ["aliases": "alias", "overwrite": true]
        )
        assertEndpoint(
            Endpoint.deleteTopicAlias(topicId: "topic 1", alias: "alias 1"),
            method: .DELETE,
            path: "/api/v1/topics/topic%201/aliases/alias%201"
        )
        assertEndpoint(
            Endpoint.mergeTopicAliases(sourceTopicId: "topic 1", destinationIdOrSlug: "topic-2"),
            method: .POST,
            path: "/api/v1/topics/topic%201/merges",
            body: ["destination_id_or_slug": "topic-2"]
        )
        assertEndpoint(
            Endpoint.spendingCategoryAttributes(topicId: "topic 1"),
            path: "/api/v1/topics/topic%201/spending-category"
        )
        assertEndpoint(
            Endpoint.updateSpendingCategoryAttributes(topicId: "topic 1", body: ["enabled": true]),
            method: .PATCH,
            path: "/api/v1/topics/topic%201/spending-category",
            body: ["enabled": true]
        )
    }

    func testPostMutationEndpointsUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.updatePost(postId: "post 1", body: UpdatePostBody(title: "Updated")),
            method: .PATCH,
            path: "/api/v1/posts/post%201",
            body: ["title": "Updated"]
        )
        assertEndpoint(Endpoint.deletePost(postId: "post 1"), method: .DELETE, path: "/api/v1/posts/post%201")
        assertEndpoint(
            Endpoint.archivePost(postId: "post 1"),
            method: .PATCH,
            path: "/api/v1/posts/post%201",
            body: ["archive": true]
        )
        assertEndpoint(
            Endpoint.unarchivePost(postId: "post 1"),
            method: .PATCH,
            path: "/api/v1/posts/post%201",
            body: ["archive": false]
        )
        assertEndpoint(
            Endpoint.addPostRating(postId: "post 1", topicId: "topic-1", rating: 5, orderIndex: 1),
            method: .POST,
            path: "/api/v1/posts/post%201/ratings",
            body: ["topic_id": "topic-1", "rating": 5, "order_index": 1]
        )
        assertEndpoint(
            Endpoint.updatePostRating(postId: "post 1", topicId: "topic 1", rating: 4),
            method: .PATCH,
            path: "/api/v1/posts/post%201/ratings/topic%201",
            body: ["rating": 4]
        )
        assertEndpoint(
            Endpoint.deletePostRating(postId: "post 1", topicId: "topic 1"),
            method: .DELETE,
            path: "/api/v1/posts/post%201/ratings/topic%201"
        )
        assertEndpoint(
            Endpoint.setPostImages(postId: "post 1", images: [.init(imageId: "image-1", orderIndex: 1)]),
            method: .PUT,
            path: "/api/v1/posts/post%201/images",
            body: ["images": [["image_id": "image-1", "order_index": 1]]]
        )
    }

    func testPasskeyPlaceholderEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.passkeyAuthOptions, method: .POST, path: "/api/v1/auth/passkeys/authentication/options")
        assertEndpoint(Endpoint.passkeyAuthVerify, method: .POST, path: "/api/v1/auth/passkeys/authentication/verify")
    }
}
