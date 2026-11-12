import Foundation
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadBoundedPreviewTests: NativeRouteSurfaceViewModelTestCase {
    private let root = "70000000-0000-7000-8000-000000005430"
    private let head = "70000000-0000-7000-8000-000000005431"
    private let child = "70000000-0000-7000-8000-000000005434"
    private let later = "70000000-0000-7000-8000-000000005436"
    private let omitted = "70000000-0000-7000-8000-000000005437"

    func testShortAndEmptyPagesContinueOpaqueCursorsAndExpandReturnedChildSignedOut() async throws {
        let path = "/api/v1/posts/\(root)/descendants"
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(root)"] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (data("short-continuation"), 200, 0),
            (data("empty-continuation"), 200, 0),
            (data("after-filtered-head"), 200, 0)
        ]
        let childPath = "/api/v1/posts/\(child)/descendants"
        CannedFeedURLProtocol.handlers[childPath] = (data("expand-preview-child"), 200)
        let model = try NativeCommentThreadViewModel(client: makeClient(), rootPostId: root, isSignedIn: false)
        await model.loadMoreHiddenDescendants(commentId: child)
        XCTAssertFalse(CannedFeedURLProtocol.hasCapturedRequest(path: childPath))
        await model.loadThread()
        XCTAssertEqual(model.descendantPosts.map(\.id), [head, child])
        XCTAssertEqual(model.commentTree.first?.children.map(\.id), [child])
        XCTAssertTrue(model.descendantPagination.hasMore)
        await model.loadMoreDescendants()
        XCTAssertEqual(model.descendantPosts.map(\.id), [head, child])
        XCTAssertTrue(model.descendantPagination.hasMore)
        await model.loadMoreHiddenDescendants(commentId: child)
        XCTAssertTrue(model.descendantPagination.hasMore)
        await model.loadMoreDescendants()
        XCTAssertEqual(model.descendantPosts.map(\.id), [head, child, omitted, later])
        XCTAssertFalse(model.descendantPagination.hasMore)
        let cursors = CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.map {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == "after" }?.value
        }
        XCTAssertEqual(cursors, try [
            nil,
            envelope("short-continuation").pageInfo.endCursor,
            envelope("empty-continuation").pageInfo.endCursor
        ])

        await model.loadMoreHiddenDescendants(commentId: child)
        await model.loadMoreHiddenDescendants(commentId: child)
        XCTAssertEqual(model.descendantPosts.map(\.id), [head, child, omitted, later])
        XCTAssertEqual(model.commentTree.first { $0.id == head }?.children.first?.children.map(\.id), [omitted])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(childPath), 1)
        XCTAssertFalse(model.subtreePagination[child]?.hasMore ?? true)
    }

    func testCappedPreviewExposesLoadMoreForSignedOutReturnedChildAndHidesItAfterCompletion() async throws {
        let response = try envelope("short-continuation")
        let post = try XCTUnwrap(response.posts[child])
        let requested = expectation(description: "Load omitted reply")
        let view = nodeView(post, metrics: response.postsMetrics ?? [:]) { id in
            XCTAssertEqual(id, self.child)
            requested.fulfill()
        }
        try view.inspect().find(button: "Load more").tap()
        await fulfillment(of: [requested], timeout: 2)

        var pagination = CursorPaginationState<Post>()
        let request = try XCTUnwrap(pagination.beginNextPage())
        pagination.complete(request, items: [], endCursor: nil, hasNextPage: false)
        var complete = nodeView(post, metrics: response.postsMetrics ?? [:]) { _ in }
        complete.subtreePagination = [post.id: pagination]
        XCTAssertThrowsError(try complete.inspect().find(button: "Load more"))
        let collapsed = nodeView(post, metrics: response.postsMetrics ?? [:], collapsed: [post.id]) { _ in }
        XCTAssertThrowsError(try collapsed.inspect().find(button: "Load more"))
        let leaf = try XCTUnwrap(try envelope("expand-preview-child").posts[omitted])
        XCTAssertThrowsError(try nodeView(leaf, metrics: [:]) { _ in }.inspect().find(button: "Load more"))
    }

    private func nodeView(
        _ post: Post, metrics: [String: PostMetrics], collapsed: Set<String> = [],
        load: @escaping (String) async -> Void
    ) -> NativeCommentThreadNodeView {
        NativeCommentThreadNodeView(
            node: NativeCommentThreadNode(post: post), rootPostId: root, rootPostType: "discussion", depth: 0,
            collapsedIds: collapsed, isSignedIn: false, canCreateVote: false, showSignIn: {},
            currentUserId: nil, hideDownCount: false, inFlightVotePostIds: [], bookmarksByPostId: [:],
            postEmbedsByPostId: [:], voteChoiceByPostId: [:], postMetricsById: metrics,
            onLoadHiddenDescendants: load, onToggleCollapse: { _ in }, onVote: { _, _ in },
            onReply: { _ in }, onQuote: { _ in }, onSave: { _ in }, onReport: { _ in },
            onEdit: { _ in }, onDelete: { _ in }, onLock: { _ in }
        )
    }

    private func data(_ suffix: String) -> Data {
        ApiFixtureLoader.data("native.comments.descendants.\(suffix)")
    }

    private func envelope(_ suffix: String) throws -> PostThreadEnvelope {
        try JSONDecoder.vouchaFixtureDecoder.decode(PostThreadEnvelope.self, from: data(suffix))
    }
}
