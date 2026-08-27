import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class BookmarkForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testBookmarkFailurePreservesRowsThenRetryAppendsWithStableRanksAndCursor() async throws {
        let path = "/api/v1/users/user-1/posts/hidden"
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (PrivateUserTestFixture.identityEnvelope(), 200)
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(id: "post-1", cursor: "bookmark-cursor", hasMore: true), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (postPage(id: "post-2", cursor: nil, hasMore: false), 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/hidden"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [0])
        XCTAssertNotNil(viewModel.bookmarkPagination.lastError)

        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [0, 1])
        XCTAssertNil(viewModel.bookmarkPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.map(\.query), [
            "limit=25",
            "limit=25&after=bookmark-cursor",
            "limit=25&after=bookmark-cursor"
        ])
    }

    private func postPage(id: String, cursor: String?, hasMore: Bool) -> Data {
        Data(
            #"{"results":[{"id":"\#(id)","slug":"\#(id)","post_type":"discussion","title":"\#(id)","markdown":"Body","created_by_id":"user-2","created_at":"2026-01-01T00:00:00Z","privacy":"public","is_anonymous":false}],"page_info":{"has_next_page":\#(hasMore),"end_cursor":\#(json(cursor))}}"#
                .utf8
        )
    }

    private func json(_ value: String?) -> String {
        value.map { #""\#($0)""# } ?? "null"
    }
}
