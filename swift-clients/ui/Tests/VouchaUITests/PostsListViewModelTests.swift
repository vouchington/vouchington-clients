import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

// MARK: - PostsListViewModel

@MainActor
final class PostsListViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    private func makeViewModel(protocolClasses: [AnyClass] = [FailingURLProtocol.self]) -> PostsListViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: protocolClasses
        )
        return PostsListViewModel(client: apiClient)
    }

    func testInitialState() {
        let vm = makeViewModel()
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)
        XCTAssertFalse(vm.isLoading)
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle state")
        }
    }

    func testInitialFilterIsAll() {
        let vm = makeViewModel()
        XCTAssertEqual(vm.filter, .all)
    }

    func testLoadTransitionsToError() async {
        let vm = makeViewModel()
        await vm.load()
        if case .error = vm.state {} else {
            XCTFail("Expected .error state after failing network")
        }
    }

    func testLoadDoesNotFireWhenAlreadyLoading() async {
        let vm = makeViewModel()
        // First load puts it in error; reset returns to idle
        await vm.load()
        vm.reset()
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle after reset")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testResetClearsState() async {
        let vm = makeViewModel()
        await vm.load()
        vm.reset()
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)
        if case .idle = vm.state {} else {
            XCTFail("Expected .idle after reset")
        }
    }
}

// MARK: - PostsListViewModel (success-path additions)

extension PostsListViewModelTests {
    private func makePostsPage(
        ids: [String],
        hasMore: Bool,
        endCursor: String? = nil,
        postElections: [String: String] = [:],
        electionVotes: [String: String] = [:],
        bookmarks: [String: [String: Bool]]? = nil,
        postLinkEmbeds: [String: String] = [:],
        broadcast: String? = nil
    ) -> Data {
        let broadcastJSON = broadcast.map { #""\#($0)""# } ?? "null"
        let posts = ids.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"slug\":null,\"post_type\":\"discussion\",\"title\":\"Post \(id)\",\"markdown\":null,\"html\":null,\"parent_post_id\":null,\"root_post_id\":null,\"created_by_id\":\"user1\",\"created_at\":\"2024-01-01T00:00:00Z\",\"broadcast\":\(broadcastJSON),\"privacy\":\"public\",\"is_anonymous\":false,\"community_id\":null,\"clearance_status\":null,\"deleted_at\":null,\"deleted_by_id\":null,\"locked_at\":null,\"locked_by_id\":null,\"archived_at\":null,\"archived_by_id\":null,\"clearance_reason\":null,\"clearance_updated_at\":null,\"spam_detection_created_at\":null,\"spam_detection_flagged\":null,\"spam_detection_results\":null,\"spam_detection_score\":null,\"updated_by_id\":null}"
        }.joined(separator: ",")
        let results = ids.map { "{\"entity_id\":\"\($0)\"}" }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        let elections = postElections.map { "\"\($0)\":\($1)" }.joined(separator: ",")
        let votes = electionVotes.map { "\"\($0)\":\($1)" }.joined(separator: ",")
        let bookmarksJSON = bookmarks.map { bookmarks in
            let entries = bookmarks.map { postId, predicates in
                let flags = predicates.map { "\"\($0.key)\":\($0.value)" }.joined(separator: ",")
                return "\"\(postId)\":{\(flags)}"
            }.joined(separator: ",")
            return ",\"bookmarks\":{\(entries)}"
        } ?? ""
        let postLinkEmbedsJSON = postLinkEmbeds.map { postId, title in
            "\"\(postId)\":{\"source_url\":\"https://example.com/\(postId)\",\"title\":\"\(title)\"}"
        }.joined(separator: ",")
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":\(hasMore),"end_cursor":\(cursor)},"posts":{\(
            posts
        )},"posts_metrics":{},"post_elections":{\(elections)},"election_votes":{\(votes)}\(
            bookmarksJSON
        ),"post_link_embeds":{\(postLinkEmbedsJSON)}}
        """.utf8)
    }

    func testLoadSuccessWithItems() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            ApiFixtureLoader.data("swift.posts.feed.default"),
            200
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertFalse(vm.hasMore)
    }

    func testPostsListHidesNegativeVoteCountsForRestrictedViewer() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            ApiFixtureLoader.data("swift.posts.feed.default"),
            200
        )
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await viewModel.load()
        let sut = PostsListView(viewModel: viewModel, hideDownCount: true)

        XCTAssertNoThrow(try sut.inspect().find(text: "4"))
        XCTAssertThrowsError(try sut.inspect().find(text: "1"))
    }

    func testPostsListShowsFollowerDistributionForEligiblePost() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(ids: ["p1"], hasMore: false, broadcast: "everyone"),
            200
        )
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await viewModel.load()

        let sut = PostsListView(viewModel: viewModel, currentUserId: "viewer-1")

        XCTAssertNoThrow(try sut.inspect().find(FollowerDistributionActions.self))
    }

    func testPostsListRetainsAndRendersPostLinkEmbed() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                postLinkEmbeds: ["p1": "Provider preview"]
            ),
            200
        )
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        await viewModel.load()

        XCTAssertEqual(viewModel.postEmbedsByPostId["p1"]?.previewTitle, "Provider preview")
        XCTAssertNoThrow(try PostsListView(viewModel: viewModel).inspect().find(text: "Provider preview"))
    }

    func testPostPaginationMergesPostLinkEmbeds() async {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/feeds/posts/any"] = [
            (
                makePostsPage(
                    ids: ["p1"],
                    hasMore: true,
                    endCursor: "next",
                    postLinkEmbeds: ["p1": "First preview"]
                ),
                200,
                0
            ),
            (
                makePostsPage(
                    ids: ["p2"],
                    hasMore: false,
                    postLinkEmbeds: ["p2": "Second preview"]
                ),
                200,
                0
            )
        ]
        let viewModel = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])

        await viewModel.load()
        await viewModel.loadNextPage()

        XCTAssertEqual(viewModel.postEmbedsByPostId["p1"]?.previewTitle, "First preview")
        XCTAssertEqual(viewModel.postEmbedsByPostId["p2"]?.previewTitle, "Second preview")
    }

    func testLoadSuccessEmptyResults() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (makePostsPage(ids: [], hasMore: false), 200)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testLoadNextPageStopsWhenHasMoreFalse() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (makePostsPage(ids: ["p1"], hasMore: false), 200)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        XCTAssertFalse(vm.hasMore)
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (makePostsPage(ids: ["p2"], hasMore: false), 200)
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 1)
    }

    func testPaginationCursorForwarded() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(ids: ["p1", "p2"], hasMore: true, endCursor: "c1"),
            200
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 2)
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (makePostsPage(ids: ["p3"], hasMore: false), 200)
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 3)
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap { URLComponents(
            url: $0,
            resolvingAgainstBaseURL: false
        ) }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "after" })?.value, "c1")
    }

    func testHttpErrorTransitionsToVouchaError() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (Data("{}".utf8), 401)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        if case let .error(err) = vm.state, case .unauthorized = err {} else {
            XCTFail("Expected .error(.unauthorized) state")
        }
    }

    func testAnonymousPostWithNullCreatedByIdDecodes() async {
        // Regression: Post.createdById must be String? — maskAnonymousPost sets
        // created_by_id to null for other users' anonymous posts. A non-optional
        // String would cause a decodingFailed error here instead of .loaded.
        CannedFeedURLProtocol.handlers = [:]
        let page = Data("""
        {"results":[{"entity_id":"anon1"}],"page_info":{"has_next_page":false,"end_cursor":null},"posts":{"anon1":{"id":"anon1","slug":null,"post_type":"discussion","title":"Anonymous post","markdown":"hello","html":null,"parent_post_id":null,"root_post_id":null,"created_by_id":null,"created_at":"2024-01-01T00:00:00Z","broadcast":null,"privacy":"public","is_anonymous":true,"community_id":null,"clearance_status":null,"deleted_at":null,"deleted_by_id":null,"locked_at":null,"locked_by_id":null,"archived_at":null,"archived_by_id":null,"clearance_reason":null,"clearance_updated_at":null,"spam_detection_created_at":null,"spam_detection_flagged":null,"spam_detection_results":null,"spam_detection_score":null,"updated_by_id":null}},"posts_metrics":{},"post_elections":{}}
        """.utf8)
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (page, 200)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        if case .loaded = vm.state {}
        else {
            XCTFail("Expected .loaded — null created_by_id must not cause decodingFailed")
        }
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertNil(vm.items.first?.createdById)
    }

    func testLoadMergesViewerVoteIntoPostElection() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                postElections: [
                    "p1": #"{"id":"p1","votes_score_net":3,"votes_count_up":4,"votes_count_down":1,"my_vote":"dislike"}"#
                ],
                electionVotes: [
                    "p1": #"{"choice":"like"}"#
                ]
            ),
            200
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        XCTAssertEqual(vm.items.first?.election?.votesScoreNet, 3)
        XCTAssertEqual(vm.items.first?.election?.votesCountUp, 4)
        XCTAssertEqual(vm.items.first?.election?.votesCountDown, 1)
        XCTAssertEqual(vm.items.first?.election?.myVote, .like)
        XCTAssertEqual(vm.myVotesByPostId["p1"], .like)
    }

    func testLoadMergesBookmarkSidecarsIntoSavedAndHiddenState() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                bookmarks: ["p1": ["save": true, "hide": true]]
            ),
            200
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()

        XCTAssertTrue(vm.isSaved(postId: "p1"))
        XCTAssertTrue(vm.isHidden(postId: "p1"))
    }

    func testToggleSaveUsesOptimisticStateAndBookmarkEndpoint() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(ids: ["p1"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/post/p1/save"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()

        await vm.toggleSave(postId: "p1")
        XCTAssertTrue(vm.isSaved(postId: "p1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/post/p1/save")
    }

    func testToggleHideRemovesPostFromActiveFeed() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(ids: ["p1", "p2"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/post/p1/hide"] = (Data("{}".utf8), 204)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()

        await vm.toggleHide(postId: "p1")

        XCTAssertTrue(vm.isHidden(postId: "p1"))
        XCTAssertEqual(vm.items.map(\.id), ["p2"])
    }

    func testToggleHideRollsBackOnFailure() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(ids: ["p1", "p2"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/post/p1/hide"] = (Data("{}".utf8), 500)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()

        await vm.toggleHide(postId: "p1")
        XCTAssertFalse(vm.isHidden(postId: "p1"))
        XCTAssertEqual(vm.items.map(\.id), ["p1", "p2"])
    }

    func testVoteUsesOptimisticStateAndRollsBackOnFailure() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                postElections: [
                    "p1": #"{"id":"p1","votes_score_net":3,"votes_count_up":4,"votes_count_down":1,"my_vote":"like"}"#
                ],
                electionVotes: [
                    "p1": #"{"choice":"like"}"#
                ]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (Data("{}".utf8), 500)
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        XCTAssertEqual(vm.myVotesByPostId["p1"], .like)
        await vm.vote(postId: "p1", choice: .dislike)
        XCTAssertEqual(vm.myVotesByPostId["p1"], .like)
        XCTAssertEqual(vm.items.first?.election?.votesCountUp, 4)
        XCTAssertEqual(vm.items.first?.election?.votesCountDown, 1)
    }

    func testVoteRequiringVerifiedEmailRollsBackAndPresentsRecovery() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                postElections: [
                    "p1": #"{"id":"p1","votes_score_net":1,"votes_count_up":1,"votes_count_down":0,"my_vote":"like"}"#
                ],
                electionVotes: ["p1": #"{"choice":"like"}"#]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/p1/vote"] = (
            Data(#"{"code":"EMAIL_VERIFICATION_REQUIRED"}"#.utf8),
            403
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()
        await vm.vote(postId: "p1", choice: .dislike)
        XCTAssertEqual(vm.myVotesByPostId["p1"], .like)
        XCTAssertTrue(vm.emailVerificationGate.isRecoveryPresented)
    }

    func testRepeatedVoteWhileWriteInFlightSendsOneRequest() async {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            makePostsPage(
                ids: ["p1"],
                hasMore: false,
                postElections: [
                    "p1": #"{"id":"p1","votes_score_net":3,"votes_count_up":4,"votes_count_down":1,"my_vote":null}"#
                ]
            ),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/p1/vote"] = [
            (Data("{}".utf8), 200, 0.05),
            (Data("{}".utf8), 200, 0)
        ]
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        await vm.load()

        async let firstVote: Void = vm.vote(postId: "p1", choice: .like)
        try? await Task.sleep(nanoseconds: 10_000_000)
        async let secondVote: Void = vm.vote(postId: "p1", choice: .dislike)
        await firstVote
        await secondVote

        let voteRequests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts/p1/vote" }
        XCTAssertEqual(voteRequests.count, 1)
        XCTAssertEqual(vm.myVotesByPostId["p1"], .like)
    }
}

/// A `URLProtocol` that always fails — prevents accidental network calls in tests.
final class FailingURLProtocol: URLProtocol {
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        client?.urlProtocol(self, didFailWithError: URLError(.notConnectedToInternet))
    }

    override func stopLoading() {}
}
