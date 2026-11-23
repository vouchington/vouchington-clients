import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class RSSFeedListViewEmptyPageTests: XCTestCase {
    private let path = "/api/v1/feeds/rss_feed_items/any"

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    private func page(ids: [String], hasMore: Bool) -> Data {
        let results = ids.map { #"{"id":"\#($0)","entity_id":"\#($0)"}"# }.joined(separator: ",")
        let items = ids.map { #""\#($0)":{"id":"\#($0)","rss_feed_id":"f","title":"Item"}"# }.joined(separator: ",")
        let cursor = hasMore ? #""next""# : "null"
        return Data(
            #"{"results":[\#(results)],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(cursor)},"rss_feed_items":{\#(items)}}"#
                .utf8
        )
    }

    private func makeSut(pages: [Data]) async -> (RSSFeedListView, RSSFeedListViewModel) {
        CannedFeedURLProtocol.queuedHandlers[path] = pages.map { ($0, 200, 0) }
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let viewModel = RSSFeedListViewModel(client: client, contentType: .news)
        await viewModel.load()
        let playback = PodcastPlaybackController(client: client, sessionManager: SessionManager(client: client))
        return (RSSFeedListView(viewModel: viewModel, playbackController: playback), viewModel)
    }

    func testCappedEmptyPagesShowLoadMoreInsteadOfTheEmptyState() async throws {
        let cap = CursorPaginationLimits.maxConsecutiveEmptyPages
        let empties = (0 ..< cap).map { _ in page(ids: [], hasMore: true) }
        let (sut, viewModel) = await makeSut(pages: empties + [page(ids: ["late"], hasMore: false)])

        XCTAssertTrue(viewModel.hasMore)
        XCTAssertNoThrow(try sut.inspect().find(button: "Load more"))
        XCTAssertThrowsError(try sut.inspect().find(ViewType.View<EmptyStateView>.self))

        await viewModel.loadNextPage()
        XCTAssertEqual(viewModel.items.map(\.id), ["late"])
        XCTAssertThrowsError(try sut.inspect().find(button: "Load more"))
    }

    func testTrueEmptyFeedShowsTheEmptyStateWithoutLoadMore() async throws {
        let (sut, viewModel) = await makeSut(pages: [page(ids: [], hasMore: false)])

        XCTAssertFalse(viewModel.hasMore)
        XCTAssertNoThrow(try sut.inspect().find(ViewType.View<EmptyStateView>.self))
        XCTAssertThrowsError(try sut.inspect().find(button: "Load more"))
    }

    func testLoadedListKeepsPaginationControlWhileMoreRemain() async throws {
        let (sut, _) = await makeSut(pages: [page(ids: ["a1"], hasMore: true)])

        XCTAssertNoThrow(try sut.inspect().find(ViewType.View<HybridPaginationControl>.self))
    }
}
