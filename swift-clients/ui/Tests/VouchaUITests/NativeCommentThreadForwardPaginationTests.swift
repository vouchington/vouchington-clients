import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCommentThreadForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testAncestorContinuationPrependsUniqueRowsAndRetriesSameCursor() async throws {
        let detailPath = "/api/v1/posts/comment-root-post"
        let ancestorsPath = "/api/v1/posts/comment-b/ancestors"
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        CannedFeedURLProtocol.handlers["\(detailPath)/descendants"] = (
            ApiFixtureLoader.data("native.comments.descendants.default"),
            200
        )
        let initialPage = try ancestorsPage(
            ids: ["comment-root-post", "comment-a", "comment-b"],
            cursor: "ancestor-cursor",
            hasMore: true
        )
        let terminalPage = try ancestorsPage(
            ids: ["comment-root-post", "comment-before", "comment-a"],
            cursor: nil,
            hasMore: false,
            extraPostId: "comment-before"
        )
        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = [
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

        await viewModel.loadPermalink(targetCommentId: "comment-b")
        if case let .error(error) = viewModel.ancestorState {
            XCTFail("Initial ancestor page failed: \(error.localizedDescription)")
        }
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-a"])
        XCTAssertTrue(viewModel.ancestorPagination.hasMore)

        await viewModel.loadMoreAncestors()
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-a"])
        XCTAssertNotNil(viewModel.ancestorPagination.lastError)

        await viewModel.loadMoreAncestors()
        XCTAssertEqual(
            viewModel.ancestorPosts.map(\.id),
            ["comment-before", "comment-a"]
        )
        XCTAssertNotNil(viewModel.postMetricsById["comment-before"])
        XCTAssertNil(viewModel.ancestorPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == ancestorsPath }.map(\.query), [
            "limit=5",
            "limit=5&after=ancestor-cursor",
            "limit=5&after=ancestor-cursor"
        ])

        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = [(initialPage, 200, 0)]
        await viewModel.loadPermalink(targetCommentId: "comment-b")
        XCTAssertNil(viewModel.postMetricsById["comment-before"])
    }

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

    private func ancestorsPage(
        ids: [String],
        cursor: String?,
        hasMore: Bool,
        extraPostId: String? = nil
    ) throws -> Data {
        let source = ApiFixtureLoader.data("native.comments.ancestors.permalink")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: source) as? [String: Any])
        object["results"] = ids.map { ["__entity_type": "post", "id": $0] }
        var posts = try XCTUnwrap(object["posts"] as? [String: Any])
        if let extraPostId {
            var post = try XCTUnwrap(posts["comment-a"] as? [String: Any])
            post["id"] = extraPostId
            posts[extraPostId] = post
            var metrics = try XCTUnwrap(object["posts_metrics"] as? [String: Any])
            var postMetrics = try XCTUnwrap(metrics["comment-a"] as? [String: Any])
            postMetrics["post_id"] = extraPostId
            metrics[extraPostId] = postMetrics
            object["posts_metrics"] = metrics
        }
        object["posts"] = posts
        let endCursor: Any = cursor.map { $0 as Any } ?? NSNull()
        object["page_info"] = [
            "has_next_page": hasMore,
            "end_cursor": endCursor,
            "start_cursor": NSNull()
        ] as [String: Any]
        return try JSONSerialization.data(withJSONObject: object)
    }
}
