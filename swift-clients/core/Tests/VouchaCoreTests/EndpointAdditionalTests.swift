import Foundation
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaModels
import XCTest

// MARK: - Endpoint

final class EndpointTests: XCTestCase {
    func testRssFeedItemsIncludesMediaType() {
        let endpoint = Endpoint.rssFeedItems(mediaType: "audio")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "media_type", value: "audio")))
    }

    func testRssFeedItemsOmitsMediaTypeWhenNil() {
        let endpoint = Endpoint.rssFeedItems()
        XCTAssertFalse(endpoint.queryItems.contains(where: { $0.name == "media_type" }))
    }

    func testPostsIncludesPostTypes() {
        let endpoint = Endpoint.posts(postTypes: "discussion")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "post_types", value: "discussion")))
    }

    func testPostsOmitsPostTypesWhenNil() {
        let endpoint = Endpoint.posts()
        XCTAssertFalse(endpoint.queryItems.contains(where: { $0.name == "post_types" }))
    }

    func testRssFeedItemsIncludesAfterCursor() {
        let endpoint = Endpoint.rssFeedItems(after: "cursor123")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "after", value: "cursor123")))
    }

    func testPostsIncludesAfterCursor() {
        let endpoint = Endpoint.posts(after: "cursor456")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "after", value: "cursor456")))
    }

    func testRssFeedItemsPath() {
        let endpoint = Endpoint.rssFeedItems(feedType: "hot")
        XCTAssertEqual(endpoint.path, "/api/v1/feeds/rss_feed_items/hot")
    }

    func testPostsPath() {
        let endpoint = Endpoint.posts(feedType: "any")
        XCTAssertEqual(endpoint.path, "/api/v1/feeds/posts/any")
    }

    func testRssFeedItemsAllParametersCombined() {
        let endpoint = Endpoint.rssFeedItems(feedType: "any", after: "c1", limit: 10, mediaType: "video")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "media_type", value: "video")))
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "after", value: "c1")))
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "limit", value: "10")))
    }

    func testVoteRssFeedItemEndpointUsesExpectedRoute() {
        assertEndpoint(
            .voteRssFeedItem(rssFeedItemId: "item 1", choice: .like),
            method: .PUT,
            path: "/api/v1/rss-feed-items/item%201/vote",
            body: ["choice": "like"]
        )
    }

    func testDiscussionCreationEndpointsUseExpectedRoutesAndIdempotencyHeaders() {
        let story = Endpoint.createStoryDiscussion(
            storyId: "story 1", idempotencyKey: "00000000-0000-4000-8000-000000000055"
        )
        assertEndpoint(
            story,
            method: .POST,
            path: "/api/v1/stories/story%201/discussions",
            body: [:]
        )
        XCTAssertEqual(story.headers["Idempotency-Key"], "00000000-0000-4000-8000-000000000055")
        let rss = Endpoint.createRssFeedItemDiscussion(
            rssFeedItemId: "item 1", idempotencyKey: "00000000-0000-4000-8000-000000000056"
        )
        assertEndpoint(
            rss,
            method: .POST,
            path: "/api/v1/rss-feed-items/item%201/discussions",
            body: [:]
        )
        XCTAssertEqual(rss.headers["Idempotency-Key"], "00000000-0000-4000-8000-000000000056")
    }

    func testDiscussionCreationEndpointsDecodeExpectedResponses() async throws {
        MockURLProtocol.handlers = [:]
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [MockURLProtocol.self]
        )

        MockURLProtocol.handlers["/api/v1/rss-feed-items/item-1/discussions"] = (
            Data("""
            {
              "post": {
                "id": "post-1",
                "slug": "link-post",
                "post_type": "link",
                "title": "Link title",
                "markdown": "",
                "html": null,
                "parent_post_id": null,
                "root_post_id": null,
                "created_by_id": "user-1",
                "created_at": "2026-01-01T00:00:00Z",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "community_id": null,
                "clearance_status": "approved",
                "deleted_at": null,
                "deleted_by_id": null,
                "locked_at": null,
                "locked_by_id": null,
                "archived_at": null,
                "archived_by_id": null,
                "clearance_reason": null,
                "clearance_updated_at": null,
                "spam_detection_created_at": null,
                "spam_detection_flagged": null,
                "spam_detection_results": null,
                "spam_detection_score": null,
                "updated_by_id": null
              }
            }
            """.utf8),
            200
        )
        let linkResult: PostEnvelope = try await client.send(
            .createRssFeedItemDiscussion(
                rssFeedItemId: "item-1",
                idempotencyKey: "00000000-0000-4000-8000-000000000057"
            )
        )
        XCTAssertEqual(linkResult.post.id, "post-1")
        XCTAssertEqual(linkResult.post.postType, .link)

        MockURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            Data("""
            {
              "post": {
                "id": "post-2",
                "slug": "story-post",
                "post_type": "story",
                "title": "Story title",
                "markdown": "",
                "html": null,
                "parent_post_id": null,
                "root_post_id": null,
                "created_by_id": "story-teller",
                "created_at": "2026-01-01T00:00:00Z",
                "broadcast": "everyone",
                "privacy": "public",
                "is_anonymous": false,
                "community_id": null,
                "clearance_status": "approved",
                "deleted_at": null,
                "deleted_by_id": null,
                "locked_at": null,
                "locked_by_id": null,
                "archived_at": null,
                "archived_by_id": null,
                "clearance_reason": null,
                "clearance_updated_at": null,
                "spam_detection_created_at": null,
                "spam_detection_flagged": null,
                "spam_detection_results": null,
                "spam_detection_score": null,
                "updated_by_id": null
              },
              "story": {
                "id": "story-1",
                "title": "Cluster title",
                "cluster_reason": "shared topic",
                "published_at": null,
                "official_rss_feed_item_id": null,
                "official_locked_at": null,
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "deleted_at": null
              },
              "postStory": {
                "post_id": "post-2",
                "story_id": "story-1",
                "initiated_by_id": "user-1",
                "created_at": "2026-01-01T00:00:00Z"
              }
            }
            """.utf8),
            200
        )
        let storyResult: StoryPostResult = try await client.send(
            .createStoryDiscussion(storyId: "story-1", idempotencyKey: "00000000-0000-4000-8000-000000000058")
        )
        XCTAssertEqual(storyResult.post.id, "post-2")
        XCTAssertEqual(storyResult.story.id, "story-1")
        XCTAssertEqual(storyResult.postStory.storyId, "story-1")
    }

    func testVoteTopicEndpointUsesExpectedRoute() {
        assertEndpoint(
            .voteTopic(topicId: "topic 1", choice: .dislike),
            method: .PUT,
            path: "/api/v1/topics/topic%201/vote",
            body: ["choice": "dislike"]
        )
    }
}

// MARK: - Endpoint (user following / followers / profile)

extension EndpointTests {
    func testUserFollowingPath() {
        let endpoint = Endpoint.userFollowing(userId: "user-123")
        XCTAssertEqual(endpoint.path, "/api/v1/users/user-123/users/following")
        XCTAssertEqual(endpoint.method, .GET)
    }

    func testUserFollowersPath() {
        let endpoint = Endpoint.userFollowers(userId: "user-456")
        XCTAssertEqual(endpoint.path, "/api/v1/users/user-456/users/followers")
        XCTAssertEqual(endpoint.method, .GET)
    }

    func testUserFollowingIncludesLimit() {
        let endpoint = Endpoint.userFollowing(userId: "u1", limit: 50)
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "limit", value: "50")))
    }

    func testUserFollowingIncludesAfterCursor() {
        let endpoint = Endpoint.userFollowing(userId: "u1", after: "cursor-1")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "100"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])
    }

    func testUserFollowersDefaultLimit() {
        let endpoint = Endpoint.userFollowers(userId: "u1")
        XCTAssertTrue(endpoint.queryItems.contains(URLQueryItem(name: "limit", value: "100")))
    }

    func testUserFollowersIncludesAfterCursor() {
        let endpoint = Endpoint.userFollowers(userId: "u1", after: "cursor-2", limit: 25)
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "after", value: "cursor-2")
        ])
    }

    func testMyProfilePath() {
        let endpoint = Endpoint.myProfile
        XCTAssertEqual(endpoint.path, "/api/v1/my/profile")
        XCTAssertEqual(endpoint.method, .GET)
    }
}

// MARK: - Podcast Endpoint paths

extension EndpointTests {
    func testPodcastEpisodeChaptersPath() {
        let endpoint = Endpoint.podcastEpisodeChapters(rssFeedItemId: "ep-789")
        XCTAssertEqual(endpoint.path, "/api/v1/podcast-episodes/ep-789/chapters")
        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertNil(endpoint.body)
    }

    func testPodcastEpisodeChaptersPercentEncodesId() {
        let endpoint = Endpoint.podcastEpisodeChapters(rssFeedItemId: "ep 789")
        XCTAssertEqual(endpoint.path, "/api/v1/podcast-episodes/ep%20789/chapters")
    }

    func testUpdatePodcastPlaybackPositionPath() {
        let endpoint = Endpoint.updatePodcastPlaybackPosition(
            rssFeedItemId: "ep-123",
            positionSeconds: 45.5,
            completed: false
        )
        XCTAssertEqual(endpoint.path, "/api/v1/podcast-episodes/ep-123/playback-position")
        XCTAssertEqual(endpoint.method, .PUT)
        XCTAssertNotNil(endpoint.body)
    }

    func testUpdatePodcastPlaybackPositionPercentEncodesId() {
        let endpoint = Endpoint.updatePodcastPlaybackPosition(
            rssFeedItemId: "ep%20test",
            positionSeconds: 0,
            completed: true
        )
        XCTAssertTrue(endpoint.path.contains("ep%2520test") || endpoint.path.contains("ep%20test"))
    }

    func testPodcastPlaybackPositionPath() {
        let endpoint = Endpoint.podcastPlaybackPosition(rssFeedItemId: "ep-456")
        XCTAssertEqual(endpoint.path, "/api/v1/podcast-episodes/ep-456/playback-position")
        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertNil(endpoint.body)
    }

    func testPodcastPlaybackPositionIdPassthrough() {
        let id = "01936eed-c123-7000-8000-000000000001"
        let endpoint = Endpoint.podcastPlaybackPosition(rssFeedItemId: id)
        XCTAssertTrue(endpoint.path.contains(id))
    }
}

// MARK: - Podcast Endpoint body encoding

final class PodcastEndpointBodyTests: XCTestCase {
    private var apiClient: APIClient!
    private struct EmptyResponse: Decodable {}

    override func setUp() {
        super.setUp()
        MockURLProtocol.handlers = [:]
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        apiClient = APIClient(
            config: config,
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [MockURLProtocol.self]
        )
    }

    func testUpdatePodcastPlaybackPositionBodyEncodes() async throws {
        let path = "/api/v1/podcast-episodes/ep-enc/playback-position"
        MockURLProtocol.handlers[path] = (Data("{}".utf8), 200)
        let endpoint = Endpoint.updatePodcastPlaybackPosition(
            rssFeedItemId: "ep-enc",
            positionSeconds: 90.0,
            completed: true
        )
        let _: EmptyResponse = try await apiClient.send(endpoint)
    }
}
