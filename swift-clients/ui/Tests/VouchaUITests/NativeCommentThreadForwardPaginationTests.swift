import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCommentThreadForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testDescendantFailurePreservesPageThenRetryAppendsDeduplicatedRows() async throws {
        let detailPath = "/api/v1/posts/comment-root-post"
        let descendantsPath = "\(detailPath)/descendants"
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        let initialPage = try descendantsPage(
            ids: ["comment-a", "comment-b"],
            cursor: "descendants-cursor",
            hasMore: true
        )
        let terminalPage = try descendantsPage(
            ids: ["comment-b", "comment-c"],
            cursor: nil,
            hasMore: false
        )
        CannedFeedURLProtocol.queuedHandlers[descendantsPath] = [
            (initialPage, 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (terminalPage, 200, 0)
        ]
        let client = try makeClient()
        let viewModel = NativeCommentThreadViewModel(
            client: client,
            rootPostId: "comment-root-post",
            currentUserId: "user-1"
        )

        await viewModel.loadThread()
        await viewModel.loadMoreDescendants()

        XCTAssertEqual(viewModel.descendantPosts.map(\.id), ["comment-a", "comment-b"])
        XCTAssertNotNil(viewModel.descendantPagination.lastError)

        await viewModel.loadMoreDescendants()

        XCTAssertEqual(viewModel.descendantPosts.map(\.id), ["comment-a", "comment-b", "comment-c"])
        XCTAssertNil(viewModel.descendantPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == descendantsPath }.map(\.query), [
            "limit=100",
            "limit=100&after=descendants-cursor",
            "limit=100&after=descendants-cursor"
        ])
    }

    private func descendantsPage(ids: [String], cursor: String?, hasMore: Bool) throws -> Data {
        let source = ApiFixtureLoader.data("native.comments.descendants.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: source) as? [String: Any])
        object["results"] = ids.map { ["__entity_type": "post", "id": $0] }
        let endCursor: Any = cursor.map { $0 as Any } ?? NSNull()
        object["page_info"] = [
            "has_next_page": hasMore,
            "end_cursor": endCursor,
            "start_cursor": NSNull()
        ] as [String: Any]
        return try JSONSerialization.data(withJSONObject: object)
    }
}
