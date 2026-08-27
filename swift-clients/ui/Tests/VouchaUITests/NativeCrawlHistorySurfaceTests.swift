import Foundation
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCrawlHistorySurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testExactCrawlsSourceSlugUsesTheSourceDetailRouteTemplate() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (sourcePage(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/source-1"] = (
            Data(#"{"can_view_latest_crawl":false}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/crawls"))
        XCTAssertEqual(route.match.template, "/source/:idOrSlug")
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/rss-feeds", "/api/v1/bookmarks/rss_feed/source-1", "/api/v1/rss-feeds/source-1"
        ])
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.hasSuffix("/crawls") })
    }

    func testPaidSourceRootOffersCrawlHistoryNavigation() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (sourcePage(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/source-1"] = (
            Data(#"{"can_view_latest_crawl":true}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.rows.last?.targetPath, "/source/native-source/crawls")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/rss-feeds/source-1")
    }

    func testSourceCrawlHistoryNavigatesToDetailAndRetriesOpaqueCursorWithoutDuplicateRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (sourcePage(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/rss-feeds/source-1/crawls"] = [
            (crawlPage(ids: ["crawl-1"], endCursor: "opaque-next", hasMore: true), 200, 0),
            (Data("{}".utf8), 503, 0),
            (crawlPage(ids: ["crawl-1", "crawl-2"], endCursor: nil, hasMore: false), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source/crawls"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        XCTAssertEqual(
            viewModel.crawlHistoryPagination.items.first?.row.targetPath,
            "/source/native-source/crawls/crawl-1"
        )

        await viewModel.loadMoreCrawlHistory()
        XCTAssertNotNil(viewModel.crawlHistoryPagination.lastError)
        XCTAssertEqual(viewModel.crawlHistoryPagination.items.map(\.id), ["crawl-1"])

        await viewModel.loadMoreCrawlHistory()
        XCTAssertEqual(viewModel.crawlHistoryPagination.items.map(\.id), ["crawl-1", "crawl-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.suffix(2).map(\.query),
            ["limit=20&after=opaque-next", "limit=20&after=opaque-next"]
        )
    }

    func testDetailSurfaceRendersCrawlPaginationRetryOnCrawlRoutes() throws {
        let entry = try entry(for: .sourceDetail)
        let viewModel = NativeRouteSurfaceViewModel(entry: entry, client: nil)
        let row = NativeForwardRow(
            id: "crawl-1",
            row: .init(icon: "doc.text.magnifyingglass", title: .verbatim("Crawl"), detail: .verbatim("200"))
        )
        viewModel.crawlHistoryPagination.reset(items: [row])
        viewModel.crawlHistoryPagination.restoreContinuation(endCursor: "opaque-next", hasMore: true)
        let request = try XCTUnwrap(viewModel.crawlHistoryPagination.beginNextPage())
        viewModel.crawlHistoryPagination.fail(request, error: .api(statusCode: 503, preconditionCode: nil))
        viewModel.state = .loaded

        let surface = NativeDetailSurface(entry: entry, viewModel: viewModel)

        XCTAssertNoThrow(try surface.inspect().find(HybridPaginationControl.self))
        XCTAssertNoThrow(try surface.inspect().find(NativeRowsSurface.self))
    }

    private func sourcePage() -> Data {
        Data(
            #"{"results":[{"id":"source-1","title":"Native Source","feed_type":"article","rss_feed_url":{"url":"https://example.com/rss"}}]}"#
                .utf8
        )
    }

    private func crawlPage(ids: [String], endCursor: String?, hasMore: Bool) -> Data {
        let results = ids.map {
            #"{"id":"\#($0)","created_at":"2026-01-01T00:00:00Z","response_code":200}"#
        }.joined(separator: ",")
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            """
            {"results":[\(results)],"page_info":{"has_next_page":\(hasMore),"start_cursor":null,"end_cursor":\(cursor)}}
            """.utf8
        )
    }
}
