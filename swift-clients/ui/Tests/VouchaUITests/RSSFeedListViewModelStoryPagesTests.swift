import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

extension RSSFeedListViewModelTests {
    func testStoryWithoutUsablePreviewKeepsEveryFeedResultVisible() async {
        for storyRelatedIds: [String: [String]] in [[:], ["story-1": []]] {
            CannedFeedURLProtocol.reset()
            CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
                makeFeedPage(
                    ids: ["primary", "secondary"], hasMore: false,
                    storyIds: ["primary": "story-1", "secondary": "story-1"],
                    storyRelatedIds: storyRelatedIds
                ), 200
            )
            let vm = makeViewModel()
            await vm.load()

            XCTAssertEqual(vm.items.map(\.id), ["primary", "secondary"])
            XCTAssertNil(vm.relatedArticles(rssFeedItemId: "primary"))
        }
    }

    func testEmptyStoryPreviewWithContinuationKeepsExpansionAvailable() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["primary", "secondary"], hasMore: false,
                storyIds: ["primary": "story-1", "secondary": "story-1"],
                storyRelatedIds: ["story-1": []], storyHasMore: true,
                storyEndCursor: "story-cursor"
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()

        XCTAssertEqual(vm.items.map(\.id), ["primary"])
        let related = try XCTUnwrap(vm.relatedArticles(rssFeedItemId: "primary"))
        XCTAssertTrue(related.pagination.hasMore)
        XCTAssertEqual(related.pagination.endCursor, "story-cursor")
    }

    func testPrefetchedStoryExpansionNeedsNoRequest() async throws {
        for preview in [["peer-1"], ["peer-3", "peer-2", "peer-1"]] {
            CannedFeedURLProtocol.reset()
            let vm = await loadStoryPreview(peers: preview)
            let related = try XCTUnwrap(vm.relatedArticles(rssFeedItemId: "primary"))
            let requests = CannedFeedURLProtocol.capturedURLs.count
            related.isExpanded.toggle()
            XCTAssertEqual(related.pagination.items.map(\.id), preview)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requests)
            XCTAssertTrue(vm.canStartStoryDiscussion(rssFeedItemId: "primary"))
            XCTAssertEqual(vm.items.map(\.id), ["primary"])
        }
    }

    func testStoryContinuationAppendsAndPreservesPreviewOnFailureAndRetry() async throws {
        let vm = await loadStoryPreview(peers: ["peer-3"])
        let related = try XCTUnwrap(vm.relatedArticles(rssFeedItemId: "primary"))
        related.isExpanded = true
        let path = "/api/v1/stories/story-1"
        CannedFeedURLProtocol.handlers[path] = (Data("{}".utf8), 500)
        await vm.loadMoreStoryArticles(rssFeedItemId: "primary")
        XCTAssertEqual(related.pagination.items.map(\.id), ["peer-3"])
        XCTAssertNotNil(related.pagination.lastError)
        XCTAssertEqual(related.pagination.endCursor, "opaque+/=")
        CannedFeedURLProtocol.handlers[path] = (storyPage(ids: ["peer-3", "peer-2", "primary"]), 200)
        await vm.loadMoreStoryArticles(rssFeedItemId: "primary")
        XCTAssertEqual(related.pagination.items.map(\.id), ["peer-3", "peer-2"])
        XCTAssertFalse(related.pagination.hasMore)
        XCTAssertTrue(related.isExpanded)
        XCTAssertNil(related.pagination.lastError)
        let query = try URLComponents(
            url: XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last),
            resolvingAgainstBaseURL: false
        )
        XCTAssertEqual(query?.queryItems?.first { $0.name == "after" }?.value, "opaque+/=")
        XCTAssertEqual(query?.queryItems?.first { $0.name == "exclude_item_id" }?.value, "primary")
        XCTAssertEqual(query?.queryItems?.first { $0.name == "limit" }?.value, "25")
        let requests = CannedFeedURLProtocol.capturedURLs.count
        await vm.loadMoreStoryArticles(rssFeedItemId: "primary")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, requests)
    }

    func testStoryContinuationAppliesRssFeedBookmarkSidecar() async {
        let vm = await loadStoryPreview(peers: ["peer-3"])
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1"] = (
            storyPage(ids: ["peer-2"], bookmarkedItemId: "peer-2"), 200
        )

        await vm.loadMoreStoryArticles(rssFeedItemId: "primary")

        XCTAssertTrue(vm.isSaved(rssFeedItemId: "peer-2"))
        XCTAssertTrue(vm.isHidden(rssFeedItemId: "peer-2"))
    }

    func testRepeatedStoryDoesNotReplacePrimaryPreviewOrCursor() async throws {
        let vm = await loadStoryPreview(peers: ["peer-3"], feedHasMore: true)
        let original = try XCTUnwrap(vm.relatedArticles(rssFeedItemId: "primary"))
        original.isExpanded = true
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["later-primary", "shared"],
                hasMore: false,
                storyIds: ["later-primary": "story-1", "shared": "story-1"],
                storyRelatedIds: ["story-1": ["peer-2", "peer-1"]],
                deliveryTypes: ["shared": "share"]
            ), 200
        )
        await vm.loadNextPage()
        XCTAssertEqual(vm.items.map(\.id), ["primary", "shared"])
        XCTAssertTrue(vm.relatedArticles(rssFeedItemId: "primary") === original)
        XCTAssertEqual(original.pagination.items.map(\.id), ["peer-3"])
        XCTAssertEqual(original.pagination.endCursor, "opaque+/=")
        XCTAssertTrue(original.isExpanded)
        XCTAssertNil(vm.relatedArticles(rssFeedItemId: "shared"))
    }

    func testResetRejectsDelayedStoryContinuation() async throws {
        let vm = await loadStoryPreview(peers: ["peer-3"])
        let related = try XCTUnwrap(vm.relatedArticles(rssFeedItemId: "primary"))
        let path = "/api/v1/stories/story-1"
        CannedFeedURLProtocol.handlers[path] = (storyPage(ids: ["peer-2"]), 200)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.discardPendingResponses() }
        let pending = Task { await vm.loadMoreStoryArticles(rssFeedItemId: "primary") }
        _ = try await barrier.wait()
        XCTAssertTrue(related.pagination.isLoading)
        await vm.loadMoreStoryArticles(rssFeedItemId: "primary")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 1)
        vm.reset()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await pending.value
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(vm.storyRelatedArticlesByStoryId.isEmpty)
        XCTAssertEqual(related.pagination.items.map(\.id), ["peer-3"])
    }

    private func loadStoryPreview(peers: [String], feedHasMore: Bool = false) async -> RSSFeedListViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            makeFeedPage(
                ids: ["primary"],
                hasMore: feedHasMore,
                endCursor: "feed-cursor",
                storyIds: ["primary": "story-1"],
                storyRelatedIds: ["story-1": peers],
                storyHasMore: true,
                storyEndCursor: "opaque+/="
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()
        return vm
    }

    private func storyPage(ids: [String], bookmarkedItemId: String? = nil) -> Data {
        let items = ids.map { "\"\($0)\":{\"id\":\"\($0)\",\"rss_feed_id\":\"feed1\",\"title\":\"Item\"}" }
            .joined(separator: ",")
        let idsJSON = ids.map { "\"\($0)\"" }.joined(separator: ",")
        let bookmarksJSON = bookmarkedItemId.map {
            ",\"rss_feed_bookmarks\":{\"\($0)\":{\"save\":true,\"hide\":true}}"
        } ?? ""
        return Data("""
        {"story":{"id":"story-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"},
         "item_ids":[\(idsJSON)],"page_info":{"has_next_page":false,"end_cursor":null},"rss_feed_items":{\(items)}\(
             bookmarksJSON
         )}
        """.utf8)
    }
}
