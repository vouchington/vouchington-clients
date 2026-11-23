import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

private let feedPath = "/api/v1/feeds/rss_feed_items/any"

extension RSSFeedListViewModelTests {
    private func queueFeed(_ pages: [Data]) {
        CannedFeedURLProtocol.queuedHandlers = [feedPath: pages.map { ($0, 200, 0) }]
        addTeardownBlock { CannedFeedURLProtocol.queuedHandlers = [:] }
    }

    func testEmptyFirstPageWithNextPageContinuesAndShowsLaterRows() async {
        queueFeed([
            makeFeedPage(ids: [], hasMore: true, endCursor: "c1"),
            makeFeedPage(ids: ["a1"], hasMore: false)
        ])
        let vm = makeViewModel()

        await vm.load()

        XCTAssertEqual(vm.items.map(\.id), ["a1"])
        XCTAssertFalse(vm.hasMore)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query?.contains("after=c1"), true)
    }

    func testFirstPageWhoseResultsAreAllDroppedContinues() async {
        queueFeed([
            makeFeedPage(ids: ["gone"], hasMore: true, endCursor: "c1", rssFeedItemIds: []),
            makeFeedPage(ids: ["a1"], hasMore: false)
        ])
        let vm = makeViewModel()

        await vm.load()

        XCTAssertEqual(vm.items.map(\.id), ["a1"])
    }

    func testEmptyLaterPageWithNextPageContinues() async {
        queueFeed([
            makeFeedPage(ids: ["a1"], hasMore: true, endCursor: "c1"),
            makeFeedPage(ids: [], hasMore: true, endCursor: "c2"),
            makeFeedPage(ids: ["a2"], hasMore: false)
        ])
        let vm = makeViewModel()

        await vm.load()
        XCTAssertEqual(vm.items.map(\.id), ["a1"])
        await vm.loadNextPage()

        XCTAssertEqual(vm.items.map(\.id), ["a1", "a2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 3)
    }

    func testConsecutiveEmptyPagesStopAtCapThenManualContinueLoadsMore() async {
        let cap = CursorPaginationLimits.maxConsecutiveEmptyPages
        let empties = (1 ... cap).map { makeFeedPage(ids: [], hasMore: true, endCursor: "c\($0)") }
        queueFeed(empties + [makeFeedPage(ids: ["late"], hasMore: false)])
        let vm = makeViewModel()

        await vm.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, cap)
        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertTrue(vm.hasMore)
        XCTAssertTrue(vm.pagination.needsManualContinuation)
        if case .loaded = vm.state {} else { XCTFail("Expected .loaded state") }

        await vm.loadNextPage()

        XCTAssertEqual(vm.items.map(\.id), ["late"])
        XCTAssertFalse(vm.hasMore)
    }

    func testEmptyPageWithoutNextPageIsTrueEmptyState() async {
        queueFeed([makeFeedPage(ids: [], hasMore: false)])
        let vm = makeViewModel()

        await vm.load()

        XCTAssertTrue(vm.items.isEmpty)
        XCTAssertFalse(vm.hasMore)
        XCTAssertFalse(vm.pagination.needsManualContinuation)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testFailureDuringContinuationStopsWithoutRetrying() async {
        CannedFeedURLProtocol.queuedHandlers = [feedPath: [
            (makeFeedPage(ids: [], hasMore: true, endCursor: "c1"), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0)
        ]]
        addTeardownBlock { CannedFeedURLProtocol.queuedHandlers = [:] }
        let vm = makeViewModel()

        await vm.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
        XCTAssertTrue(vm.hasPaginationError)
    }
}
