import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RSSFeedListViewModelStoryDiscussionCoverageTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeViewModel(contentType: ContentType = .news) -> RSSFeedListViewModel {
        let apiClient = APIClient(
            config: AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return RSSFeedListViewModel(client: apiClient, contentType: contentType)
    }

    func testStoryDiscussionDestinationBuildsNativeRouteMatch() {
        let destination = StoryDiscussionDestination(postId: "post-1", postType: .story)

        XCTAssertEqual(destination.id, "story|post-1")
        XCTAssertEqual(destination.routeMatch.path, "/story/post-1")
        XCTAssertEqual(destination.routeMatch.template, "/story/:id")
        XCTAssertEqual(destination.routeMatch.params["id"], "post-1")
    }

    func testStoryDiscussionDestinationReturnsStoredDestination() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            makeStoryPostResult(postId: "post-story", storyId: "story-1"),
            201
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "missing"), nil)
        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "story-primary"), destination)
    }

    func testStoryDiscussionDestinationIsNewsOnly() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]],
                storyPostIds: ["story-1": "post-story"]
            ),
            200
        )
        let vm = makeViewModel(contentType: .video)
        await vm.load()

        XCTAssertNil(vm.storyDiscussionDestination(rssFeedItemId: "story-primary"))
    }

    func testStartReturnsNilWhenStoryDiscussionIsUnavailable() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary"]]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertNil(destination)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
    }

    func testStartReloadsAndReturnsNilOnConflict() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            Data(#"{"code":"STORY_DISCUSSION_EXISTS"}"#.utf8),
            409
        )
        let vm = makeViewModel()
        await vm.load()
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]],
                storyPostIds: ["story-1": "post-existing"]
            ),
            200
        )

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertEqual(destination?.postId, "post-existing")
        XCTAssertEqual(destination?.postType, .story)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            [
                "/api/v1/feeds/rss_feed_items/any",
                "/api/v1/stories/story-1/discussions",
                "/api/v1/feeds/rss_feed_items/any"
            ]
        )
        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "story-primary"), destination)
    }

    func testStartSetsErrorWhenStoryEndpointFails() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            Data(#"{"code":"SERVER_ERROR"}"#.utf8),
            500
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertNil(destination)
        if case .error = vm.state {} else {
            XCTFail("Expected story discussion failure to surface an error state")
        }
    }

    func testFallbackSetsErrorWhenLinkDiscussionFails() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            Data(#"{"code":"FEED_NOT_DISCOVERABLE"}"#.utf8),
            403
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/story-primary/discussions"] = (
            Data(#"{"code":"SERVER_ERROR"}"#.utf8),
            500
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertNil(destination)
        if case .error = vm.state {} else {
            XCTFail("Expected fallback failure to surface an error state")
        }
    }

    func testFallbackDiscussionDestinationSurvivesReload() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (
            Data(#"{"code":"FEED_NOT_DISCOVERABLE"}"#.utf8),
            403
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/story-primary/discussions"] = (
            makePostEnvelope(postId: "post-link", postType: "link"),
            201
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")
        await vm.reload()

        XCTAssertEqual(destination?.postId, "post-link")
        XCTAssertEqual(destination?.postType, .link)
        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "story-primary"), destination)
        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
    }

    func testStartingStateTracksQueuedRequest() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary", "story-peer"],
                storyIds: ["story-primary": "story-1", "story-peer": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        let path = "/api/v1/stories/story-1/discussions"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (makeStoryPostResult(postId: "post-story", storyId: "story-1"), 201, 0)
        ]
        let vm = makeViewModel()
        await vm.load()

        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let request = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        async let pendingDestination = vm.startStoryDiscussion(rssFeedItemId: "story-primary")
        _ = try await request.wait()
        XCTAssertTrue(vm.isStartingStoryDiscussion(rssFeedItemId: "story-primary"))
        XCTAssertTrue(vm.isStartingStoryDiscussion(rssFeedItemId: "story-peer"))
        let duplicateDestination = await vm.startStoryDiscussion(rssFeedItemId: "story-peer")
        XCTAssertNil(duplicateDestination)

        CannedFeedURLProtocol.releaseResponse(path: path)
        let destination = await pendingDestination
        XCTAssertEqual(destination?.postId, "post-story")
        XCTAssertFalse(vm.isStartingStoryDiscussion(rssFeedItemId: "story-primary"))
        XCTAssertFalse(vm.isStartingStoryDiscussion(rssFeedItemId: "story-peer"))
    }

    func testStartingStateSurvivesResetWhileRequestIsPending() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary", "story-peer"],
                storyIds: ["story-primary": "story-1", "story-peer": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/stories/story-1/discussions"] = [
            (makeStoryPostResult(postId: "post-story", storyId: "story-1"), 201, 1.0)
        ]
        let vm = makeViewModel()
        await vm.load()

        async let pendingDestination = vm.startStoryDiscussion(rssFeedItemId: "story-primary")
        for _ in 0 ..< 20 where !vm.isStartingStoryDiscussion(rssFeedItemId: "story-primary") {
            try? await Task.sleep(nanoseconds: 10_000_000)
        }
        vm.reset()
        vm.storyIdsByItemId = ["story-primary": "story-1", "story-peer": "story-1"]
        vm.storyRelatedArticlesByStoryId = ["story-1": StoryRelatedArticles(
            primaryItemId: "story-primary",
            items: [RssFeedItem(
                id: "story-peer",
                rssFeedId: "feed1",
                title: nil,
                description: nil,
                content: nil,
                link: nil,
                publishedAt: nil,
                creator: nil,
                categories: nil,
                mediaContent: nil
            )],
            pageInfo: .init(hasNextPage: false)
        )]

        XCTAssertTrue(vm.isStartingStoryDiscussion(rssFeedItemId: "story-primary"))
        XCTAssertTrue(vm.isStartingStoryDiscussion(rssFeedItemId: "story-peer"))
        let duplicateDestination = await vm.startStoryDiscussion(rssFeedItemId: "story-peer")
        XCTAssertNil(duplicateDestination)

        let destination = await pendingDestination
        XCTAssertEqual(destination?.postId, "post-story")
    }

    private func makeFeedPage(
        ids: [String],
        storyIds: [String: String],
        storyRelatedIds: [String: [String]],
        storyPostIds: [String: String] = [:]
    ) -> Data {
        let items = Array(Set(ids + storyRelatedIds.values.flatMap { $0 })).sorted().map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"rss_feed_id\":\"feed1\",\"title\":\"Item \(id)\",\"description\":null,\"content\":null,\"link\":null,\"published_at\":null,\"creator\":null,\"categories\":[],\"media_content\":null}"
        }.joined(separator: ",")
        let results = ids.map { id in
            "{\"id\":\"\(id)\",\"entity_id\":\"\(id)\",\"story_id\":\"\(storyIds[id] ?? "")\"}"
        }.joined(separator: ",")
        let storyMembers = storyRelatedIds.map { storyId, memberIds in
            let primary = ids.first { storyIds[$0] == storyId }
            let members = memberIds.filter { $0 != primary }.map { "\"\($0)\"" }.joined(separator: ",")
            return "\"\(storyId)\":{\"item_ids\":[\(members)],\"page_info\":{\"has_next_page\":false,\"end_cursor\":null}}"
        }.joined(separator: ",")
        let storyPosts = storyPostIds.map { storyId, postId in
            "\"\(storyId)\":\"\(postId)\""
        }.joined(separator: ",")
        return Data("""
        {
          "results":[\(results)],
          "page_info":{"has_next_page":false,"end_cursor":null},
          "rss_feed_items":{\(items)},
          "story_member_pages":{\(storyMembers)},
          "story_post_ids":{\(storyPosts)}
        }
        """.utf8)
    }

    private func makeStoryPostResult(postId: String, storyId: String) -> Data {
        Data("""
        {
          "post":{\(postJSON(id: postId, postType: "story"))},
          "story":{
            "id":"\(storyId)",
            "title":"Story",
            "cluster_reason":null,
            "published_at":null,
            "official_rss_feed_item_id":null,
            "official_locked_at":null,
            "created_at":"2026-01-01T00:00:00Z",
            "updated_at":"2026-01-01T00:00:00Z",
            "deleted_at":null
          },
          "postStory":{
            "post_id":"\(postId)",
            "story_id":"\(storyId)",
            "initiated_by_id":"user-1",
            "created_at":"2026-01-01T00:00:00Z"
          }
        }
        """.utf8)
    }

    private func makePostEnvelope(postId: String, postType: String) -> Data {
        Data("""
        {
          "post":{\(postJSON(id: postId, postType: postType))}
        }
        """.utf8)
    }

    private func postJSON(id: String, postType: String) -> String {
        """
        "id":"\(id)",
        "slug":null,
        "post_type":"\(postType)",
        "title":"Created",
        "markdown":null,
        "html":null,
        "parent_post_id":null,
        "root_post_id":null,
        "created_by_id":"user-1",
        "created_at":"2026-01-01T00:00:00Z",
        "broadcast":null,
        "privacy":"public",
        "is_anonymous":false,
        "community_id":null,
        "clearance_status":null,
        "deleted_at":null,
        "deleted_by_id":null,
        "locked_at":null,
        "locked_by_id":null,
        "archived_at":null,
        "archived_by_id":null,
        "clearance_reason":null,
        "clearance_updated_at":null,
        "spam_detection_created_at":null,
        "spam_detection_flagged":null,
        "spam_detection_results":null,
        "spam_detection_score":null,
        "updated_by_id":null
        """
    }
}
