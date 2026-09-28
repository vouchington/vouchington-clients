import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

// MARK: - RSSFeedListViewModel

@MainActor
final class RSSFeedListViewModelTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    func makeViewModel(contentType: ContentType = .news) -> RSSFeedListViewModel {
        let config = AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return RSSFeedListViewModel(client: apiClient, contentType: contentType)
    }

    func makeFeedPage(
        ids: [String],
        hasMore: Bool,
        endCursor: String? = nil,
        resultEntityIds: [String]? = nil,
        rssFeedItemIds: [String]? = nil,
        itemElections: [String: (score: Double, up: Int, down: Int)] = [:],
        electionVotes: [String: ElectionVoteChoice] = [:],
        bookmarks: [String: [String: Bool]]? = nil,
        thumbnailURLs: [String: String] = [:],
        storyIds: [String: String] = [:],
        storyRelatedIds: [String: [String]] = [:],
        storyHasMore: Bool = false,
        storyEndCursor: String? = nil,
        deliveryTypes: [String: String] = [:],
        storyPostIds: [String: String] = [:]
    ) -> Data {
        let resolvedResultEntityIds = resultEntityIds ?? ids
        let resolvedRssFeedItemIds =
            Array(Set((rssFeedItemIds ?? resolvedResultEntityIds) + storyRelatedIds.values.flatMap { $0 })).sorted()

        let items = resolvedRssFeedItemIds.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"rss_feed_id\":\"feed1\",\"title\":\"Item \(id)\",\"description\":null,\"content\":null,\"link\":null,\"published_at\":null,\"creator\":null,\"categories\":[],\"media_content\":null}"
        }.joined(separator: ",")
        let results = ids.enumerated().map { index, id in
            guard resolvedResultEntityIds.indices.contains(index) else {
                return "{\"id\":\"\(id)\"}"
            }
            let itemId = resolvedResultEntityIds[index]
            let storyJSON = storyIds[itemId].map { ",\"story_id\":\"\($0)\"" } ?? ""
            let delivery = deliveryTypes[id].map { ",\"delivery_type\":\"\($0)\"" } ?? ""
            return "{\"id\":\"\(id)\",\"entity_id\":\"\(itemId)\"\(storyJSON)\(delivery)}"
        }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        let elections = itemElections.map { itemId, election in
            """
            "\(itemId)":{"votes_score_net":\(election.score),"votes_count_up":\(election
                .up),"votes_count_down":\(election.down)}
            """
        }.joined(separator: ",")
        let votes = electionVotes.map { itemId, choice in
            """
            "\(itemId)":{"__entity_type":"election_vote","user_id":"user-1","entity_id":"\(itemId)","choice":"\(
                choice.rawValue
            )","created_at":"2026-01-01T00:00:00Z"}
            """
        }.joined(separator: ",")
        let bookmarksJSON = bookmarks.map { bookmarks in
            let entries = bookmarks.map { itemId, predicates in
                let flags = predicates.map { "\"\($0.key)\":\($0.value)" }.joined(separator: ",")
                return "\"\(itemId)\":{\(flags)}"
            }.joined(separator: ",")
            return ",\"bookmarks\":{\(entries)}"
        } ?? ""
        let thumbnails = thumbnailURLs.map { itemId, url in
            "\"\(itemId)\":\"\(url)\""
        }.joined(separator: ",")
        let storyMembers = storyRelatedIds.map { storyId, memberIds in
            let primary = ids.first { storyIds[$0] == storyId }
            let storyCursor = storyEndCursor.map { "\"\($0)\"" } ?? "null"
            let members = memberIds.filter { $0 != primary }.map { "\"\($0)\"" }.joined(separator: ",")
            return "\"\(storyId)\":{\"item_ids\":[\(members)],\"page_info\":{\"has_next_page\":\(storyHasMore),\"end_cursor\":\(storyCursor)}}"
        }.joined(separator: ",")
        let storyPosts = storyPostIds.map { storyId, postId in
            "\"\(storyId)\":\"\(postId)\""
        }.joined(separator: ",")
        return Data("""
        {
          "results":[\(results)],
          "page_info":{"has_next_page":\(hasMore),"end_cursor":\(cursor)},
          "rss_feed_items":{\(items)},
          "rss_feed_item_thumbnail_url":{\(thumbnails)},
          "rss_feed_item_elections":{\(elections)},
          "election_votes":{\(votes)},
          "story_member_pages":{\(storyMembers)},
          "story_post_ids":{\(storyPosts)}\(bookmarksJSON)
        }
        """.utf8)
    }

    func testLoadSuccessTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["a1"], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.items.count, 1)
        XCTAssertFalse(vm.hasMore)
    }

    func testMediaTypePassedInRequest() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            ApiFixtureLoader.data("swift.rss-feed-items.feed.default"),
            200
        )
        let vm = makeViewModel(contentType: .video)
        await vm.loadNextPage()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.items.count, 1)
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap { URLComponents(
            url: $0,
            resolvingAgainstBaseURL: false
        ) }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "media_type" })?.value, "video")
    }

    func testPaginationAccumulatesItems() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["p1", "p2"], hasMore: true, endCursor: "c2"),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 2)
        XCTAssertTrue(vm.hasMore)
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["p3"], hasMore: false),
            200
        )
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 3)
        XCTAssertFalse(vm.hasMore)
    }

    func testEmptyResultsTransitionsToLoaded() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: [], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertTrue(vm.items.isEmpty)
    }

    func testReloadClearsAndRefetches() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["r1"], hasMore: false),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.count, 1)
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["r2", "r3"], hasMore: false),
            200
        )
        await vm.reload()
        XCTAssertEqual(vm.items.count, 2)
    }

    func testLoadMergesItemSidecarsAndViewerVote() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["v1"],
                hasMore: false,
                itemElections: ["v1": (score: 6.5, up: 7, down: 1)],
                electionVotes: ["v1": .like]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertEqual(vm.election(for: "v1")?.votesScoreNet, 6.5)
        XCTAssertEqual(vm.election(for: "v1")?.myVote, .like)
        XCTAssertEqual(vm.myVotesByItemId["v1"], .like)
    }

    func testLoadAppliesThumbnailSidecarToItems() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["thumb-1"],
                hasMore: false,
                thumbnailURLs: ["thumb-1": "/sideload/thumb.jpg"]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.items.first?.thumbnailURL, "http://localhost:2999/sideload/thumb.jpg")
        XCTAssertEqual(vm.items.first?.displayThumbnailURLString, "http://localhost:2999/sideload/thumb.jpg")
    }

    func testLoadUsesEntityIdForSharedFeedRows() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["share-event-1"],
                hasMore: false,
                resultEntityIds: ["item-1"],
                rssFeedItemIds: ["item-1"],
                thumbnailURLs: ["item-1": "/sideload/item-1.jpg"]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()

        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded state")
        }
        XCTAssertEqual(vm.items.map(\.id), ["item-1"])
        XCTAssertEqual(vm.items.first?.title, "Item item-1")
        XCTAssertEqual(vm.items.first?.thumbnailURL, "http://localhost:2999/sideload/item-1.jpg")
    }

    func testVoteAppliesOptimisticallyAndRollsBack() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["v1"], hasMore: false, itemElections: ["v1": (score: 6.5, up: 7, down: 1)]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/v1/vote"] = (Data("{}".utf8), 204)
        let vm = makeViewModel()
        await vm.load()

        await vm.vote(rssFeedItemId: "v1", choice: .like)
        XCTAssertEqual(vm.myVotesByItemId["v1"], .like)
        XCTAssertEqual(vm.election(for: "v1")?.votesCountUp, 8)
        XCTAssertEqual(vm.election(for: "v1")?.votesCountDown, 1)

        await vm.vote(rssFeedItemId: "v1", choice: nil)
        XCTAssertNil(vm.myVotesByItemId["v1"])
        XCTAssertEqual(vm.election(for: "v1")?.votesCountUp, 7)

        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/v1/vote"] = (Data("{}".utf8), 500)
        await vm.vote(rssFeedItemId: "v1", choice: .dislike)
        XCTAssertNil(vm.myVotesByItemId["v1"])
        XCTAssertEqual(vm.election(for: "v1")?.votesCountDown, 1)
    }

    func testLoadMergesBookmarkSidecarsIntoSavedAndHiddenState() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["i1"],
                hasMore: false,
                bookmarks: ["i1": ["save": true, "hide": true]]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()
        XCTAssertTrue(vm.isSaved(rssFeedItemId: "i1"))
        XCTAssertTrue(vm.isHidden(rssFeedItemId: "i1"))
    }

    func testToggleSaveUsesBookmarkEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["i1"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed_item/i1/save"] = (Data("{}".utf8), 204)
        let vm = makeViewModel()
        await vm.load()

        await vm.toggleSave(rssFeedItemId: "i1")
        XCTAssertTrue(vm.isSaved(rssFeedItemId: "i1"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/bookmarks/rss_feed_item/i1/save")
    }

    func testToggleHideRemovesItemFromActiveFeed() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["i1", "i2"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed_item/i1/hide"] = (Data("{}".utf8), 204)
        let vm = makeViewModel()
        await vm.load()

        await vm.toggleHide(rssFeedItemId: "i1")

        XCTAssertTrue(vm.isHidden(rssFeedItemId: "i1"))
        XCTAssertEqual(vm.items.map(\.id), ["i2"])
    }

    func testToggleHideRollsBackOnError() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["i1", "i2"], hasMore: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed_item/i1/hide"] = (Data("{}".utf8), 500)
        let vm = makeViewModel()
        await vm.load()

        await vm.toggleHide(rssFeedItemId: "i1")
        XCTAssertFalse(vm.isHidden(rssFeedItemId: "i1"))
        XCTAssertEqual(vm.items.map(\.id), ["i1", "i2"])
    }
}

// MARK: - RSSFeedListViewModel (All Feed path)

@MainActor
final class RSSFeedListViewModelAllFeedTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    func makeViewModel(feedSource: FeedSource) -> RSSFeedListViewModel {
        let config = AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key")
        let apiClient = APIClient(
            config: config,
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        return RSSFeedListViewModel(client: apiClient, contentType: .news, feedSource: feedSource)
    }

    func makeFeedPage(ids: [String], hasMore: Bool) -> Data {
        let items = ids.map { id in
            "\"\(id)\":{\"id\":\"\(id)\",\"rss_feed_id\":\"feed1\",\"title\":\"Item \(id)\",\"description\":null,\"content\":null,\"link\":null,\"published_at\":null,\"creator\":null,\"categories\":[],\"media_content\":null}"
        }.joined(separator: ",")
        let results = ids.map { "{\"id\":\"\($0)\"}" }.joined(separator: ",")
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":\(hasMore),"end_cursor":null},"rss_feed_items":{\(items)}}
        """.utf8)
    }

    func testAllFeedHitsGlobalEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items"] = (
            makeFeedPage(ids: ["a1", "a2"], hasMore: false),
            200
        )
        let vm = makeViewModel(feedSource: .all)
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded")
        }
        XCTAssertEqual(vm.items.count, 2)
        let path = CannedFeedURLProtocol.capturedURLs.first?.path
        XCTAssertEqual(path, "/api/v1/rss-feed-items")
    }

    func testYourFeedHitsPersonalizedEndpoint() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(ids: ["y1"], hasMore: false),
            200
        )
        let vm = makeViewModel(feedSource: .your)
        await vm.load()
        if case .loaded = vm.state {} else {
            XCTFail("Expected .loaded")
        }
        XCTAssertEqual(vm.items.count, 1)
        let path = CannedFeedURLProtocol.capturedURLs.first?.path
        XCTAssertEqual(path, "/api/v1/feeds/rss_feed_items/any")
    }

    func testDistinctShareDeliveriesKeepSharedItemHydration() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["direct", "share-A", "share-B"],
                hasMore: false,
                resultEntityIds: ["item-X", "item-X", "item-X"],
                rssFeedItemIds: ["item-X", "peer-1"],
                thumbnailURLs: ["item-X": "/sideload/item-X.jpg"],
                storyIds: ["item-X": "story-1"],
                storyRelatedIds: ["story-1": ["peer-1"]],
                deliveryTypes: ["direct": "direct", "share-A": "share", "share-B": "share"]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.feedRows.map(\.deliveryId), ["direct", "share-A", "share-B"])
        XCTAssertEqual(vm.feedRows.map(\.showsStory), [true, false, false])
        XCTAssertEqual(vm.items.map(\.id), ["item-X", "item-X", "item-X"])
        XCTAssertEqual(vm.items.map(\.title), ["Item item-X", "Item item-X", "Item item-X"])
        XCTAssertEqual(
            vm.items.map(\.thumbnailURL),
            Array(repeating: "http://localhost:2999/sideload/item-X.jpg", count: 3)
        )
        XCTAssertEqual(vm.relatedArticles(rssFeedItemId: "item-X")?.primaryItemId, "item-X")
    }

    func testContinuationKeepsNewDeliveriesAndDropsRepeatedOnes() async {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["direct"],
                hasMore: true,
                endCursor: "c2",
                resultEntityIds: ["item-X"],
                rssFeedItemIds: ["item-X", "peer-1"],
                storyIds: ["item-X": "story-1"],
                storyRelatedIds: ["story-1": ["peer-1"]],
                deliveryTypes: ["direct": "direct"]
            ),
            200
        )
        let vm = makeViewModel()
        await vm.loadNextPage()
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["direct", "share-A", "direct-Y"],
                hasMore: false,
                resultEntityIds: ["item-X", "item-X", "item-Y"],
                rssFeedItemIds: ["item-X", "item-Y"],
                storyIds: ["item-X": "story-1", "item-Y": "story-1"],
                deliveryTypes: ["direct": "direct", "share-A": "share", "direct-Y": "direct"]
            ),
            200
        )
        await vm.loadNextPage()

        XCTAssertEqual(vm.feedRows.map(\.deliveryId), ["direct", "share-A"])
        XCTAssertEqual(vm.feedRows.map(\.showsStory), [true, false])
        XCTAssertEqual(vm.items.map(\.id), ["item-X", "item-X"])
        XCTAssertEqual(vm.relatedArticles(rssFeedItemId: "item-X")?.primaryItemId, "item-X")
    }
}
