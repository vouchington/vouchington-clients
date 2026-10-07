import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class RSSFeedListViewModelStoryDiscussionTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    private func makeViewModel(contentType: ContentType = .news) -> RSSFeedListViewModel {
        let apiClient = APIClient(
            config: AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return RSSFeedListViewModel(client: apiClient, contentType: contentType)
    }

    func testAvailabilityRequiresStoryPeerAndNoExistingPost() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["story-primary", "posted-primary", "single-primary"],
                storyIds: [
                    "story-primary": "story-1",
                    "posted-primary": "story-2",
                    "single-primary": "story-3"
                ],
                storyRelatedIds: [
                    "story-1": ["story-primary", "story-peer"],
                    "story-2": ["posted-primary", "posted-peer"],
                    "story-3": ["single-primary"]
                ],
                storyPostIds: ["story-2": "post-existing"]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()

        XCTAssertTrue(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "posted-primary"))
        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "single-primary"))
    }

    func testStartUsesStoryEndpointAndHidesAction() async {
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

        XCTAssertEqual(destination?.postId, "post-story")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/stories/story-1/discussions")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "POST")
        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "story-primary")?.postId, "post-story")
        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
    }

    func testStartFallsBackToRssFeedItemDiscussionWhenFeedIsNotDiscoverable() async {
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
            makePostEnvelope(id: "post-link"),
            201
        )
        let vm = makeViewModel()
        await vm.load()

        let destination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertEqual(destination?.postId, "post-link")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.path),
            ["/api/v1/stories/story-1/discussions", "/api/v1/rss-feed-items/story-primary/discussions"]
        )
        XCTAssertEqual(vm.storyPostIdsByStoryId["story-1"], "post-link")
        XCTAssertEqual(vm.storyDiscussionDestination(rssFeedItemId: "story-primary")?.postId, "post-link")
        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
    }

    func testFallbackVerificationRecoveryRequiresExplicitRetry() async {
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
        CannedFeedURLProtocol.queuedHandlers["/api/v1/rss-feed-items/story-primary/discussions"] = [
            (Data(#"{"code":"EMAIL_VERIFICATION_REQUIRED"}"#.utf8), 403, 0),
            (makePostEnvelope(id: "post-link"), 201, 0)
        ]
        let vm = makeViewModel()
        await vm.load()
        CannedFeedURLProtocol.capturedURLs = []

        let firstDestination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertNil(firstDestination)
        XCTAssertTrue(vm.emailVerificationGate.isRecoveryPresented)
        XCTAssertTrue(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/stories/story-1/discussions", "/api/v1/rss-feed-items/story-primary/discussions"]
        )

        vm.emailVerificationGate.dismissRecovery()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)

        let retryDestination = await vm.startStoryDiscussion(rssFeedItemId: "story-primary")

        XCTAssertEqual(retryDestination?.postId, "post-link")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 4)
    }

    func testStoryDiscussionActionIsNewsOnly() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any?media_type=video"] = (
            makeFeedPage(
                ids: ["story-primary"],
                storyIds: ["story-primary": "story-1"],
                storyRelatedIds: ["story-1": ["story-primary", "story-peer"]]
            ),
            200
        )
        let vm = makeViewModel(contentType: .video)
        await vm.load()

        XCTAssertFalse(vm.canStartStoryDiscussion(rssFeedItemId: "story-primary"))
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

    private func makePostEnvelope(id: String) -> Data {
        Data("""
        {"post":{\(postJSON(id: id, postType: "link"))}}
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

    private func postJSON(id: String, postType: String) -> String {
        """
        "id":"\(id)",
        "slug":null,
        "post_type":"\(postType)",
        "title":"Created",
        "markdown":null,
        "html":null,
        "parent_id":null,
        "root_id":null,
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
