import Foundation
@testable import VouchaFeatures
import XCTest

extension RSSFeedListViewModelTests {
    func testSuccessfulPrimaryHideRejectsRepeatedItemFromHeldFeedPage() async throws {
        let feedPath = "/api/v1/feeds/rss_feed_items/any"
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["before", "primary"], hasMore: true, endCursor: "feed-cursor",
                storyIds: ["primary": "story-1"], storyRelatedIds: ["story-1": ["peer-3"]]
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["primary", "later-sibling", "unrelated"], hasMore: false,
                storyIds: ["primary": "story-1", "later-sibling": "story-1"]
            ), 200
        )
        let feedBarrier = CannedFeedURLProtocol.requestBarrier(path: feedPath, method: "GET")
        CannedFeedURLProtocol.suspendResponse(path: feedPath)
        defer { CannedFeedURLProtocol.discardPendingResponses() }
        let pendingFeed = Task { await vm.loadNextPage() }
        do {
            _ = try await feedBarrier.wait()
        } catch {
            pendingFeed.cancel()
            CannedFeedURLProtocol.releaseResponse(path: feedPath)
            CannedFeedURLProtocol.discardPendingResponses()
            await pendingFeed.value
            throw error
        }

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed_item/primary/hide"] = (
            Data("{}".utf8), 204
        )
        await vm.toggleHide(rssFeedItemId: "primary")
        XCTAssertEqual(vm.items.map(\.id), ["before"])
        XCTAssertTrue(vm.isHidden(rssFeedItemId: "primary"))

        CannedFeedURLProtocol.releaseResponse(path: feedPath)
        await pendingFeed.value
        XCTAssertEqual(vm.items.map(\.id), ["before", "later-sibling", "unrelated"])
        XCTAssertNil(vm.relatedArticles(rssFeedItemId: "primary"))
        XCTAssertFalse(vm.hasMore)
    }

    func testFailedPrimaryHideRestoresGroupAfterHeldFeedRepeatsPrimary() async throws {
        let feedPath = "/api/v1/feeds/rss_feed_items/any"
        let hidePath = "/api/v1/bookmarks/rss_feed_item/primary/hide"
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["before", "primary"], hasMore: true, endCursor: "feed-cursor",
                storyIds: ["primary": "story-1"], storyRelatedIds: ["story-1": ["peer-3"]],
                storyHasMore: true, storyEndCursor: "story-cursor"
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()
        let original = vm.relatedArticles(rssFeedItemId: "primary")
        XCTAssertNotNil(original)
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["primary", "later-sibling", "unrelated"], hasMore: false,
                storyIds: ["primary": "story-1", "later-sibling": "story-1"]
            ), 200
        )
        CannedFeedURLProtocol.handlers[hidePath] = (Data("{}".utf8), 500)
        let feedBarrier = CannedFeedURLProtocol.requestBarrier(path: feedPath, method: "GET")
        CannedFeedURLProtocol.suspendResponse(path: feedPath)
        CannedFeedURLProtocol.suspendResponse(path: hidePath)
        defer { CannedFeedURLProtocol.discardPendingResponses() }
        let pendingFeed = Task { await vm.loadNextPage() }
        do {
            _ = try await feedBarrier.wait()
        } catch {
            pendingFeed.cancel()
            CannedFeedURLProtocol.releaseResponse(path: feedPath)
            CannedFeedURLProtocol.discardPendingResponses()
            await pendingFeed.value
            throw error
        }

        let hideBarrier = CannedFeedURLProtocol.requestBarrier(path: hidePath, method: "PUT")
        let pendingHide = Task { await vm.toggleHide(rssFeedItemId: "primary") }
        do {
            _ = try await hideBarrier.wait()
        } catch {
            pendingFeed.cancel()
            pendingHide.cancel()
            CannedFeedURLProtocol.releaseResponse(path: feedPath)
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            CannedFeedURLProtocol.discardPendingResponses()
            await pendingFeed.value
            await pendingHide.value
            throw error
        }
        CannedFeedURLProtocol.releaseResponse(path: feedPath)
        await pendingFeed.value
        XCTAssertEqual(vm.items.map(\.id), ["before", "later-sibling", "unrelated"])

        CannedFeedURLProtocol.releaseResponse(path: hidePath)
        await pendingHide.value
        XCTAssertEqual(vm.items.map(\.id), ["before", "primary", "unrelated"])
        XCTAssertTrue(vm.relatedArticles(rssFeedItemId: "primary") === original)
        XCTAssertEqual(Set(original?.pagination.items.map(\.id) ?? []), Set(["peer-3", "later-sibling"]))
        XCTAssertEqual(original?.pagination.endCursor, "story-cursor")
        XCTAssertFalse(vm.isHidden(rssFeedItemId: "primary"))
    }
}
