import ViewInspector
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SourcesListViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    private func makeViewModel(userId: String? = "user-abc", feedType: String? = "article") -> SourcesListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return SourcesListViewModel(client: apiClient, userId: userId, feedType: feedType)
    }

    private func makeSourcesPage(
        ids: [String],
        feedType: String = "article",
        bookmarks: [String: [String: Bool]]? = nil,
        topicElections: [String: (score: Double, up: Int, down: Int)] = [:],
        electionVotes: [String: ElectionVoteChoice] = [:]
    ) -> Data {
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
              "feed_type":"\(feedType)",
              "rss_feed_url":{"url":"https://example.com/\(id).xml","canonical_url_id":null},
              "home_page_url":null,
              "hostname":{"hostname":"example\(id).com","id":"h\(id)"},
              "topic":{"id":"t1","slug":"tech","topic_type":"topic","name":"Tech","hostname_id":null,"hostname":null,"logo_image_id":null,"hero_image_id":null,"homepage_url_id":null,"lingua_rs_detected_language":null,"referral_program_id":null,"referral_program_slug":null,"rewards_program_id":null},
              "publisher_type":null,
              "podcast_show":null
            }
            """
        }.joined(separator: ",")
        let topicElectionsJSON = topicElections.map { topicId, election in
            """
            "\(topicId)":{"votes_score_net":\(election.score),"votes_count_up":\(election
                .up),"votes_count_down":\(election.down)}
            """
        }.joined(separator: ",")
        let electionVotesJSON = electionVotes.map { topicId, choice in
            """
            "\(topicId)":{"__entity_type":"election_vote","user_id":"user-1","entity_id":"\(topicId)","choice":"\(choice
                .rawValue)","created_at":"2026-01-01T00:00:00Z"}
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
        {"results":[\(items)],"topic_elections":{\(topicElectionsJSON)},"election_votes":{\(electionVotesJSON)}\(
            bookmarksJSON
        )}
        """.utf8)
    }

    func testInitialStateIsIdle() {
        let vm = makeViewModel()
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle state")
        }
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertEqual(vm.scope, .your)
    }

    func testViewRendersEmptyStateMessage() throws {
        let vm = makeViewModel(userId: nil)
        let sut = SourcesListView(viewModel: vm, scope: .your, navigationTitle: "Your Sources")

        XCTAssertEqual(try sut.inspect().find(text: "No Sources").string(), "No Sources")
        XCTAssertEqual(
            try sut.inspect().find(text: "Follow some sources to see them here.").string(),
            "Follow some sources to see them here."
        )
    }

    func testViewRendersLoadedSourceWithFollowAction() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"], feedType: "article"),
            200
        )
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()

        let sut = SourcesListView(viewModel: vm, scope: .all, navigationTitle: "All Sources")
        XCTAssertEqual(try sut.inspect().find(text: "Source s1").string(), "Source s1")
        XCTAssertNoThrow(try sut.inspect().find(button: "Follow"))
    }

    func testLoadYourSourcesHitsUserEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1", "s2"]),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded")
        }
        XCTAssertEqual(vm.items.count, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/users/user-abc/rss-feeds/following")
    }

    func testLoadAllSourcesHitsGlobalEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s3"]),
            200
        )
        let vm = makeViewModel()
        vm.scope = .all
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded")
        }
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/rss-feeds")
    }

    func testLoadAllSourcesInitializesFollowedSourceIdsFromBookmarks() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(
                ids: ["s1", "s2"],
                bookmarks: ["s2": ["follow": true], "t1": ["follow": true]],
                topicElections: ["t1": (score: 4.5, up: 5, down: 1)],
                electionVotes: ["t1": .like]
            ),
            200
        )
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        XCTAssertFalse(vm.isFollowing(sourceId: "s1"))
        XCTAssertTrue(vm.isFollowing(sourceId: "s2"))
        XCTAssertTrue(vm.isFollowingTopic(topicId: "t1"))
        XCTAssertFalse(vm.isFollowingTopic(topicId: "s2"))
        XCTAssertEqual(vm.topicElection(for: "t1")?.votesScoreNet, 4.5)
        XCTAssertEqual(vm.topicElection(for: "t1")?.myVote, .like)
        XCTAssertEqual(vm.myVotesByTopicId["t1"], .like)
    }

    func testLoadAllSourcesInitializesMutedIdsFromBookmarks() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(
                ids: ["s1", "s2"],
                bookmarks: ["s1": ["mute": true], "t1": ["mute": true]]
            ),
            200
        )
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()

        XCTAssertTrue(vm.isMuted(sourceId: "s1"))
        XCTAssertTrue(vm.isMutedTopic(topicId: "t1"))
        XCTAssertFalse(vm.isMuted(sourceId: "s2"))
    }

    func testReloadOverwritesTopicVoteFromServer() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(
                ids: ["s1"],
                topicElections: ["t1": (score: 4.5, up: 5, down: 1)],
                electionVotes: ["t1": .like]
            ),
            200
        )
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        XCTAssertEqual(vm.myVotesByTopicId["t1"], .like)

        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(
                ids: ["s1"],
                topicElections: ["t1": (score: 4.5, up: 5, down: 1)],
                electionVotes: ["t1": .dislike]
            ),
            200
        )
        await vm.reload()

        XCTAssertEqual(vm.myVotesByTopicId["t1"], .dislike)
        XCTAssertEqual(vm.topicElection(for: "t1")?.myVote, .dislike)
    }

    func testLoadAllSourcesMergesPageScopedBookmarkState() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s2"], bookmarks: ["s2": ["follow": true]]),
            200
        )
        let vm = makeViewModel(feedType: nil)
        await vm.load()

        vm.scope = .all
        await vm.load()

        XCTAssertTrue(vm.isFollowing(sourceId: "s1"))
        XCTAssertTrue(vm.isFollowing(sourceId: "s2"))
    }

    func testLoadYourSourcesPreservesCachedTopicFollowState() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"], bookmarks: ["t1": ["follow": true]]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s2"], bookmarks: ["s2": ["follow": true]]),
            200
        )
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        XCTAssertTrue(vm.isFollowingTopic(topicId: "t1"))

        vm.scope = .your
        await vm.load()

        XCTAssertTrue(vm.isFollowingTopic(topicId: "t1"))
        XCTAssertFalse(vm.isFollowingTopic(topicId: "s2"))
    }

    func testLoadAllSourcesWithoutBookmarksPreservesFollowingState() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1", "s2"]),
            200
        )
        let vm = makeViewModel(feedType: nil)
        await vm.load()
        XCTAssertTrue(vm.isFollowing(sourceId: "s1"))

        vm.scope = .all
        await vm.load()
        XCTAssertTrue(vm.isFollowing(sourceId: "s1"))
        XCTAssertFalse(vm.isFollowing(sourceId: "s2"))
    }

    func testToggleFollowUsesOptimisticUpdateAndRollback() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        XCTAssertFalse(vm.isFollowing(sourceId: "s1"))

        await vm.toggleFollow(sourceId: "s1")
        XCTAssertTrue(vm.isFollowing(sourceId: "s1"))

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 500)
        await vm.toggleFollow(sourceId: "s1")
        XCTAssertTrue(vm.isFollowing(sourceId: "s1"))
    }

    func testToggleFollowIgnoresConcurrentDuplicateRequests() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        CannedFeedURLProtocol.capturedURLs = []

        async let first: Void = vm.toggleFollow(sourceId: "s1")
        async let second: Void = vm.toggleFollow(sourceId: "s1")
        _ = await (first, second)

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/bookmarks/rss_feed/s1/follow" }.count,
            1
        )
    }

    func testToggleFollowRemovesRowFromYourSources() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1", "s2"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/follow"] = (Data("{}".utf8), 204)
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.items.map(\.id), ["s1", "s2"])

        await vm.toggleFollow(sourceId: "s1")
        XCTAssertEqual(vm.items.map(\.id), ["s2"])
        XCTAssertFalse(vm.isFollowing(sourceId: "s1"))
    }

    func testToggleFollowDoesNothingWhenUserIdIsNil() async {
        let vm = makeViewModel(userId: nil, feedType: nil)
        await vm.toggleFollow(sourceId: "s1")
        XCTAssertFalse(vm.isFollowing(sourceId: "s1"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testToggleMuteUsesOptimisticUpdateAndRollback() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/mute"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()

        await vm.toggleMute(sourceId: "s1")
        XCTAssertTrue(vm.isMuted(sourceId: "s1"))

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/s1/mute"] = (Data("{}".utf8), 500)
        await vm.toggleMute(sourceId: "s1")
        XCTAssertTrue(vm.isMuted(sourceId: "s1"))
    }

    func testToggleFollowTopicUsesOptimisticUpdateAndRollback() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"], bookmarks: ["t1": ["follow": true]]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/t1/follow"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()
        XCTAssertTrue(vm.isFollowingTopic(topicId: "t1"))

        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        await vm.toggleFollowTopic(topicId: "t1")
        XCTAssertFalse(vm.isFollowingTopic(topicId: "t1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/topic/t1/follow")
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "DELETE")

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/t1/follow"] = (Data("{}".utf8), 500)
        await vm.toggleFollowTopic(topicId: "t1")
        XCTAssertFalse(vm.isFollowingTopic(topicId: "t1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "PUT")
    }

    func testToggleMuteTopicUsesBookmarkEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"], bookmarks: ["t1": ["follow": true]]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/t1/mute"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()

        await vm.toggleMuteTopic(topicId: "t1")
        XCTAssertTrue(vm.isMutedTopic(topicId: "t1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/topic/t1/mute")
    }

    func testToggleFollowTopicDoesNothingWithoutTopicOrUser() async {
        let anonymous = makeViewModel(userId: nil, feedType: nil)
        await anonymous.toggleFollowTopic(topicId: "t1")

        let signedIn = makeViewModel(feedType: nil)
        await signedIn.toggleFollowTopic(topicId: nil)

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testVoteTopicAppliesOptimisticallyAndRollsBack() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s1"], topicElections: ["t1": (score: 2.25, up: 3, down: 1)]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/topics/t1/vote"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(feedType: nil)
        vm.scope = .all
        await vm.load()

        await vm.vote(topicId: "t1", choice: .like)
        XCTAssertEqual(vm.myVotesByTopicId["t1"], .like)
        XCTAssertEqual(vm.topicElection(for: "t1")?.votesCountUp, 4)

        await vm.vote(topicId: "t1", choice: nil)
        XCTAssertNil(vm.myVotesByTopicId["t1"])
        XCTAssertEqual(vm.topicElection(for: "t1")?.votesCountUp, 3)

        CannedFeedURLProtocol.handlers["/api/v1/topics/t1/vote"] = (Data("{}".utf8), 500)
        await vm.vote(topicId: "t1", choice: .dislike)
        XCTAssertNil(vm.myVotesByTopicId["t1"])
        XCTAssertEqual(vm.topicElection(for: "t1")?.votesCountDown, 1)
    }

    func testLoadWithNilUserIdTransitionsToLoaded() async {
        let vm = makeViewModel(userId: nil)
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded when userId is nil")
        }
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testSwitchingScopeLoadsCorrectEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            makeSourcesPage(ids: ["s2", "s3"]),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.items.count, 1)

        vm.scope = .all
        await vm.load()
        XCTAssertEqual(vm.items.count, 2)
    }

    func testYourSourcesPassesFeedTypeToServer() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["article1"], feedType: "article"),
            200
        )
        let vm = makeViewModel(feedType: "article")
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded")
        }
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "feed_type" })?.value, "article")
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertEqual(vm.items.first?.feedType, "article")
    }

    func testReloadClearsAndRefetches() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s1"]),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.items.count, 1)

        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (
            makeSourcesPage(ids: ["s2", "s3"]),
            200
        )
        await vm.reload()
        XCTAssertEqual(vm.items.count, 2)
    }

    func testErrorTransitionsToErrorState() async {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-abc/rss-feeds/following"] = (Data("{}".utf8), 401)
        let vm = makeViewModel()
        await vm.load()
        if case .error = vm.state {} else {
            XCTFail("Expected .error state")
        }
    }
}
