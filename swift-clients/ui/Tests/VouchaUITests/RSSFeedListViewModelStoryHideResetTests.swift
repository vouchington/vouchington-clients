import Foundation
@testable import VouchaFeatures
import XCTest

extension RSSFeedListViewModelTests {
    func testPrimaryHideRollbackRejectsReplacementStoryResponse() async throws {
        let feedPath = "/api/v1/feeds/rss_feed_items/any"
        let hidePath = "/api/v1/bookmarks/rss_feed_item/primary/hide"
        let storyPath = "/api/v1/stories/story-1"
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["primary"], hasMore: true, endCursor: "feed-cursor",
                storyIds: ["primary": "story-1"], storyRelatedIds: ["story-1": ["old-peer"]]
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()
        let original = vm.relatedArticles(rssFeedItemId: "primary")
        XCTAssertNotNil(original)
        CannedFeedURLProtocol.handlers[hidePath] = (Data("{}".utf8), 500)
        let hideBarrier = CannedFeedURLProtocol.requestBarrier(path: hidePath, method: "PUT")
        CannedFeedURLProtocol.suspendResponse(path: hidePath)
        defer { CannedFeedURLProtocol.discardPendingResponses() }
        let pendingHide = Task { await vm.toggleHide(rssFeedItemId: "primary") }
        do {
            _ = try await hideBarrier.wait()
        } catch {
            pendingHide.cancel()
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            CannedFeedURLProtocol.discardPendingResponses()
            await pendingHide.value
            throw error
        }

        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["later-primary"], hasMore: false,
                storyIds: ["later-primary": "story-1"],
                storyRelatedIds: ["story-1": ["replacement-peer"]],
                storyHasMore: true, storyEndCursor: "replacement-cursor"
            ), 200
        )
        await vm.loadNextPage()
        guard let replacement = vm.relatedArticles(rssFeedItemId: "later-primary") else {
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            await pendingHide.value
            XCTFail("Expected replacement group while the primary hide is pending")
            return
        }
        CannedFeedURLProtocol.handlers[storyPath] = (Data("""
        {"story":{"id":"story-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"},
         "item_ids":["stale"],"page_info":{"has_next_page":false,"end_cursor":null},
         "rss_feed_items":{"stale":{"id":"stale","rss_feed_id":"feed1","title":"Stale"}}}
        """.utf8), 200)
        let storyBarrier = CannedFeedURLProtocol.requestBarrier(path: storyPath, method: "GET")
        CannedFeedURLProtocol.suspendResponse(path: storyPath)
        let pendingStory = Task { await vm.loadMoreStoryArticles(rssFeedItemId: "later-primary") }
        do {
            _ = try await storyBarrier.wait()
        } catch {
            pendingStory.cancel()
            pendingHide.cancel()
            CannedFeedURLProtocol.releaseResponse(path: storyPath)
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            CannedFeedURLProtocol.discardPendingResponses()
            await pendingStory.value
            await pendingHide.value
            throw error
        }
        XCTAssertTrue(replacement.pagination.isLoading)

        CannedFeedURLProtocol.releaseResponse(path: hidePath)
        await pendingHide.value
        XCTAssertFalse(replacement.pagination.isLoading)
        XCTAssertTrue(vm.relatedArticles(rssFeedItemId: "primary") === original)
        XCTAssertEqual(original?.pagination.endCursor, "replacement-cursor")

        CannedFeedURLProtocol.releaseResponse(path: storyPath)
        await pendingStory.value
        XCTAssertEqual(vm.items.map(\.id), ["primary"])
        XCTAssertEqual(
            Set(original?.pagination.items.map(\.id) ?? []),
            Set(["old-peer", "later-primary", "replacement-peer"])
        )
        XCTAssertFalse(original?.pagination.items.contains { $0.id == "stale" } ?? true)
    }

    func testOldHideFailureAfterReloadCannotRestoreOldGroupOrClearNewHideGuard() async throws {
        let feedPath = "/api/v1/feeds/rss_feed_items/any"
        let hidePath = "/api/v1/bookmarks/rss_feed_item/primary/hide"
        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["primary"], hasMore: true, endCursor: "feed-cursor",
                storyIds: ["primary": "story-1"], storyRelatedIds: ["story-1": ["old-peer"]],
                storyHasMore: true, storyEndCursor: "old-cursor"
            ), 200
        )
        let vm = makeViewModel()
        await vm.load()
        let oldGroup = vm.relatedArticles(rssFeedItemId: "primary")
        XCTAssertNotNil(oldGroup)
        CannedFeedURLProtocol.handlers[hidePath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.suspendResponse(path: hidePath)
        defer { CannedFeedURLProtocol.discardPendingResponses() }

        let oldBarrier = CannedFeedURLProtocol.requestBarrier(path: hidePath, method: "PUT")
        let oldHide = Task { await vm.toggleHide(rssFeedItemId: "primary") }
        do {
            _ = try await oldBarrier.wait()
        } catch {
            oldHide.cancel()
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            CannedFeedURLProtocol.discardPendingResponses()
            await oldHide.value
            throw error
        }

        CannedFeedURLProtocol.handlers[feedPath] = (
            makeFeedPage(
                ids: ["primary"], hasMore: false,
                storyIds: ["primary": "story-1"], storyRelatedIds: ["story-1": ["fresh-peer"]],
                storyHasMore: true, storyEndCursor: "fresh-cursor"
            ), 200
        )
        await vm.reload()
        let freshGroup = vm.relatedArticles(rssFeedItemId: "primary")
        XCTAssertNotNil(freshGroup)
        XCTAssertFalse(freshGroup === oldGroup)

        let newBarrier = CannedFeedURLProtocol.requestBarrier(path: hidePath, method: "PUT")
        let newHide = Task { await vm.toggleHide(rssFeedItemId: "primary") }
        do {
            _ = try await newBarrier.wait()
        } catch {
            oldHide.cancel()
            newHide.cancel()
            CannedFeedURLProtocol.releaseResponse(path: hidePath)
            CannedFeedURLProtocol.discardPendingResponses()
            await oldHide.value
            await newHide.value
            throw error
        }
        XCTAssertEqual(CannedFeedURLProtocol.suspendedResponseCount(path: hidePath), 2)

        CannedFeedURLProtocol.releaseOldestResponse(path: hidePath)
        await oldHide.value
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(vm.isHidden(rssFeedItemId: "primary"))
        XCTAssertTrue(vm.inFlightBookmarkKeys.contains("primary|hide"))
        await vm.toggleHide(rssFeedItemId: "primary")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(hidePath), 2)

        CannedFeedURLProtocol.releaseResponse(path: hidePath)
        await newHide.value
        XCTAssertEqual(vm.items.map(\.id), ["primary"])
        XCTAssertTrue(vm.relatedArticles(rssFeedItemId: "primary") === freshGroup)
        XCTAssertEqual(freshGroup?.pagination.items.map(\.id), ["fresh-peer"])
        XCTAssertEqual(freshGroup?.pagination.endCursor, "fresh-cursor")
        XCTAssertFalse(vm.isHidden(rssFeedItemId: "primary"))
    }
}
