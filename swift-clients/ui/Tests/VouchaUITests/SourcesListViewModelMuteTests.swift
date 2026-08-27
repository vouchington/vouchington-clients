import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SourcesListViewModelMuteTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    private func makeViewModel(userId: String? = "user-abc", feedType: String? = nil) -> SourcesListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return SourcesListViewModel(client: apiClient, userId: userId, feedType: feedType)
    }

    private func makeSourcesPage(ids: [String], bookmarks: [String: [String: Bool]]? = nil) -> Data {
        let items = ids.map { id in
            """
            {
              "id":"\(id)",
              "title":"Source \(id)",
              "is_enabled":true,
              "is_discoverable":true,
              "etag":null,
              "last_modified_at":null,
              "last_fetched_at":null,
              "feed_type":"article",
              "rss_feed_url":{"url":"https://example.com/\(id).xml","canonical_url_id":null},
              "home_page_url":null,
              "hostname":{"hostname":"example\(id).com","id":"h\(id)"},
              "topic":{"id":"t1","slug":"tech","topic_type":"topic","name":"Tech","hostname_id":null,"hostname":null,"logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,"lingua_rs_detected_language":null,"referral_program_id":null,"referral_program_slug":null,"rewards_program_id":null},
              "publisher_type":null,
              "podcast_show":null
            }
            """
        }.joined(separator: ",")
        let bookmarksJSON = bookmarks.map { bookmarks in
            let entries = bookmarks.map { sourceId, predicates in
                let flags = predicates.map { "\"\($0.key)\":\($0.value)" }.joined(separator: ",")
                return "\"\(sourceId)\":{\(flags)}"
            }.joined(separator: ",")
            return ",\"bookmarks\":{\(entries)}"
        } ?? ""
        return Data("""
        {"results":[\(items)],"topic_elections":{},"election_votes":{}\(bookmarksJSON)}
        """.utf8)
    }

    func testToggleMuteClearsSourceFollowStateAndRollsBackOnFailure() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/mute"] = (Data("{}".utf8), 204)
        let successViewModel = makeViewModel()
        await successViewModel.load()

        await successViewModel.toggleMute(sourceId: "s1")
        XCTAssertFalse(successViewModel.isFollowing(sourceId: "s1"))
        XCTAssertTrue(successViewModel.items.isEmpty)
        XCTAssertTrue(successViewModel.isMuted(sourceId: "s1"))

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/mute"] = (Data("{}".utf8), 500)
        let rollbackViewModel = makeViewModel()
        await rollbackViewModel.load()

        await rollbackViewModel.toggleMute(sourceId: "s1")
        XCTAssertTrue(rollbackViewModel.isFollowing(sourceId: "s1"))
        XCTAssertEqual(rollbackViewModel.items.map(\.id), ["s1"])
        XCTAssertFalse(rollbackViewModel.isMuted(sourceId: "s1"))
    }

    func testToggleMuteIsIgnoredWhileFollowToggleIsInFlight() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/bookmarks/rss_feed/s1/follow"] = [
            (Data("{}".utf8), 204, 0.05)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/mute"] = (Data("{}".utf8), 204)
        let viewModel = makeViewModel(feedType: nil)
        viewModel.scope = .all
        await viewModel.load()
        CannedFeedURLProtocol.capturedURLs = []

        let followTask = Task { await viewModel.toggleFollow(sourceId: "s1") }
        await Task.yield()
        await viewModel.toggleMute(sourceId: "s1")
        await followTask.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/bookmarks/rss_feed/s1/mute" }.count,
            0
        )
        XCTAssertFalse(viewModel.isMuted(sourceId: "s1"))
    }

    func testToggleMuteTopicClearsFollowStateAndRollsBackOnFailure() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(
                ids: ["s1"],
                bookmarks: ["t1": ["follow": true]]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/t1/mute"] = (Data("{}".utf8), 204)
        let successViewModel = makeViewModel(feedType: nil)
        successViewModel.scope = .all
        await successViewModel.load()

        await successViewModel.toggleMuteTopic(topicId: "t1")
        XCTAssertFalse(successViewModel.isFollowingTopic(topicId: "t1"))
        XCTAssertTrue(successViewModel.isMutedTopic(topicId: "t1"))

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/t1/mute"] = (Data("{}".utf8), 500)
        let rollbackViewModel = makeViewModel(feedType: nil)
        rollbackViewModel.scope = .all
        await rollbackViewModel.load()

        await rollbackViewModel.toggleMuteTopic(topicId: "t1")
        XCTAssertTrue(rollbackViewModel.isFollowingTopic(topicId: "t1"))
        XCTAssertFalse(rollbackViewModel.isMutedTopic(topicId: "t1"))
    }

    func testReloadAllSourcesClearsMutedStateWhenBookmarksAreRemoved() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1", "s2"], bookmarks: ["s1": ["mute": true], "t1": ["mute": true]]),
            200
        )
        let viewModel = makeViewModel(feedType: nil)
        viewModel.scope = .all
        await viewModel.load()
        XCTAssertTrue(viewModel.isMuted(sourceId: "s1"))
        XCTAssertTrue(viewModel.isMutedTopic(topicId: "t1"))

        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1", "s2"], bookmarks: [:]),
            200
        )
        await viewModel.reload()

        XCTAssertFalse(viewModel.isMuted(sourceId: "s1"))
        XCTAssertFalse(viewModel.isMutedTopic(topicId: "t1"))
    }
}
