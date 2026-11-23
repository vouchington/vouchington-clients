import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityNewsEmptyPageTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/communities/builders/news"

    func testEmptyFirstNewsPageWithNextPageContinuesToPageTwoRows() async throws {
        queue([newsPage(ids: [], cursor: "c1", hasMore: true), newsPage(ids: ["news-2"])])
        let (client, viewModel) = try makeViewModel()

        let rows = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)

        XCTAssertEqual(rows.map(\.id), ["news-2"])
        XCTAssertFalse(viewModel.rowPagination.hasMore)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
    }

    func testEmptyLaterNewsPageWithNextPageContinues() async throws {
        queue([
            newsPage(ids: ["news-1"], cursor: "c1", hasMore: true),
            newsPage(ids: [], cursor: "c2", hasMore: true),
            newsPage(ids: ["news-3"])
        ])
        let (client, viewModel) = try makeViewModel()

        _ = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)
        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.rowPagination.items.map(\.id), ["news-1", "news-3"])
        XCTAssertEqual(viewModel.summary.rows.count, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 3)
    }

    func testConsecutiveEmptyNewsPagesStopAtCapWithManualContinuation() async throws {
        let cap = CursorPaginationLimits.maxConsecutiveEmptyPages
        queue((1 ... cap).map { newsPage(ids: [], cursor: "c\($0)", hasMore: true) }
            + [newsPage(ids: ["late"])])
        let (client, viewModel) = try makeViewModel()

        let rows = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)

        XCTAssertTrue(rows.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, cap)
        XCTAssertTrue(viewModel.rowPagination.hasMore)
        XCTAssertTrue(viewModel.rowPagination.needsManualContinuation)

        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.rowPagination.items.map(\.id), ["late"])
        XCTAssertEqual(viewModel.summary.rows.count, 1)
    }

    func testEmptyNewsPageWithoutNextPageIsTrueEmptyState() async throws {
        queue([newsPage(ids: [])])
        let (client, viewModel) = try makeViewModel()

        let rows = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)

        XCTAssertTrue(rows.isEmpty)
        XCTAssertFalse(viewModel.rowPagination.hasMore)
        XCTAssertFalse(viewModel.rowPagination.needsManualContinuation)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testFailureOnLaterEmptyContinuationKeepsRowsAndStops() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (newsPage(ids: ["news-1"], cursor: "c1", hasMore: true), 200, 0),
            (newsPage(ids: [], cursor: "c2", hasMore: true), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0)
        ]
        let (client, viewModel) = try makeViewModel()

        _ = try await viewModel.loadInitialRows(client: client, tab: .news, revision: 0)
        await viewModel.loadMoreRows()

        XCTAssertEqual(viewModel.rowPagination.items.map(\.id), ["news-1"])
        XCTAssertNotNil(viewModel.rowPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 3)
    }

    private func makeViewModel() throws -> (APIClient, CommunityDetailViewModel) {
        let client = try makeClient()
        return (client, CommunityDetailViewModel(client: client, slug: "builders", initialTab: .news))
    }

    private func queue(_ pages: [Data]) {
        CannedFeedURLProtocol.queuedHandlers[path] = pages.map { ($0, 200, 0) }
    }

    private func newsPage(ids: [String], cursor: String? = nil, hasMore: Bool = false) -> Data {
        let results = ids.map { #"{"id":"\#($0)","entity_id":"\#($0)"}"# }.joined(separator: ",")
        let items = ids.map { #""\#($0)":{"id":"\#($0)","title":"News item"}"# }.joined(separator: ",")
        let end = cursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(end)},"rss_feed_items":{\#(items)}}"#
                .utf8
        )
    }
}
