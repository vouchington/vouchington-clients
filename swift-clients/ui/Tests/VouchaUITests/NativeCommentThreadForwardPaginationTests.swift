import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeCommentThreadForwardPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testRootPermalinkDoesNotInsertRootAmongComments() async throws {
        let rootPath = "/api/v1/posts/\(CommentFixture.root)"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/descendants"] = try (
            descendantsPage(ids: [CommentFixture.a], cursor: nil, hasMore: false), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/ancestors"] = try (
            ancestorsPage(ids: [CommentFixture.root], cursor: nil, hasMore: false), 200
        )
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: CommentFixture.root, currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.root)

        XCTAssertEqual(viewModel.rootPost?.id, CommentFixture.root)
        XCTAssertEqual(viewModel.descendantPosts.map(\.id), [CommentFixture.a])
        XCTAssertEqual(viewModel.commentTree.map(\.post.id), [CommentFixture.a])
    }

    func testSlugBackedPermalinkOmitsCanonicalRootFromAncestorTrail() async throws {
        let rootPath = "/api/v1/posts/story-slug"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/descendants"] = try (
            descendantsPage(ids: [CommentFixture.a, CommentFixture.b], cursor: nil, hasMore: false), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.b)/ancestors"] = (
            ApiFixtureLoader.data("native.comments.ancestors.permalink"), 200
        )
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: "story-slug", currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)

        XCTAssertEqual(viewModel.rootPost?.id, CommentFixture.root)
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), [CommentFixture.a])
        XCTAssertEqual(viewModel.commentTree.first?.children.map(\.post.id), [CommentFixture.b])
    }

    func testPermalinkTargetOutsideFirstDescendantPageAppearsOnceAndSurvivesContinuation() async throws {
        let rootPath = "/api/v1/posts/\(CommentFixture.root)"
        let descendantsPath = "\(rootPath)/descendants"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.queuedHandlers[descendantsPath] = try [
            (descendantsPage(ids: [CommentFixture.a], cursor: "next-descendants", hasMore: true), 200, 0),
            (descendantsPage(ids: [CommentFixture.b], cursor: nil, hasMore: false), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.b)/ancestors"] = (
            ApiFixtureLoader.data("native.comments.ancestors.permalink"), 200
        )
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: CommentFixture.root, currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        XCTAssertEqual(viewModel.descendantPosts.map(\.id), [CommentFixture.a, CommentFixture.b])
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), [CommentFixture.a])
        XCTAssertEqual(viewModel.commentTree.first?.children.map(\.post.id), [CommentFixture.b])

        await viewModel.loadMoreDescendants()
        XCTAssertEqual(viewModel.descendantPosts.filter { $0.id == CommentFixture.b }.count, 1)
        XCTAssertEqual(viewModel.commentTree.first?.children.map(\.post.id), [CommentFixture.b])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == descendantsPath }.map(\.query),
            ["limit=100", "limit=100&after=next-descendants"]
        )
    }

    func testEditedAncestorSurvivesPrependingAnotherAncestorPage() async throws {
        let rootPath = "/api/v1/posts/\(CommentFixture.root)"
        let ancestorsPath = "/api/v1/posts/\(CommentFixture.b)/ancestors"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/descendants"] = (
            ApiFixtureLoader.data("native.comments.descendants.default"), 200
        )
        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = try [
            (ancestorsPage(
                ids: [CommentFixture.root, CommentFixture.a, CommentFixture.b],
                cursor: "earlier-ancestors", hasMore: true
            ), 200, 0),
            (ancestorsPage(
                ids: [CommentFixture.root, "comment-before"],
                cursor: nil, hasMore: false, extraPostId: "comment-before"
            ), 200, 0)
        ]
        var mutation = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data("native.comments.ancestors.permalink"))
                as? [String: Any]
        )
        var posts = try XCTUnwrap(mutation["posts"] as? [String: Any])
        var editedPost = try XCTUnwrap(posts[CommentFixture.a] as? [String: Any])
        editedPost["markdown"] = "Updated ancestor"
        editedPost["html"] = "<p>Updated ancestor</p>"
        posts[CommentFixture.a] = editedPost
        mutation["posts"] = posts
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.a)"] = try (
            JSONSerialization.data(withJSONObject: ["post": editedPost]), 200
        )
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: CommentFixture.root, currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        await viewModel.edit(postId: CommentFixture.a, markdown: "Updated ancestor")
        XCTAssertEqual(viewModel.ancestorPosts.first?.markdown, "Updated ancestor")
        await viewModel.loadMoreAncestors()

        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-before", CommentFixture.a])
        XCTAssertEqual(viewModel.ancestorPosts.last?.markdown, "Updated ancestor")
        let edited = viewModel.ancestorPagination.items.first { $0.id == CommentFixture.a }
        XCTAssertEqual(edited?.markdown, "Updated ancestor")
        XCTAssertEqual(viewModel.ancestorPagination.endCursor, nil)
    }

    func testVotedAncestorSurvivesPrependingAnotherAncestorPage() async throws {
        let rootPath = "/api/v1/posts/\(CommentFixture.root)"
        let ancestorsPath = "/api/v1/posts/\(CommentFixture.b)/ancestors"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/descendants"] = try (
            descendantsPage(ids: [CommentFixture.b], cursor: nil, hasMore: false), 200
        )
        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = try [
            (ancestorsPage(
                ids: [CommentFixture.root, CommentFixture.a, CommentFixture.b],
                cursor: "earlier-ancestors", hasMore: true
            ), 200, 0),
            (ancestorsPage(
                ids: [CommentFixture.root, "comment-before"],
                cursor: nil, hasMore: false, extraPostId: "comment-before"
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.a)/vote"] = (Data("{}".utf8), 200)
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: CommentFixture.root, currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        XCTAssertNil(viewModel.ancestorPosts.first?.election)
        XCTAssertEqual(viewModel.postElectionsById[CommentFixture.a]?.votesCountUp, 8)
        XCTAssertEqual(viewModel.voteChoicesByPostId[CommentFixture.a], .like)
        await viewModel.vote(postId: CommentFixture.a, choice: .dislike)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.myVote, .dislike)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.votesCountUp, 7)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.votesCountDown, 1)
        await viewModel.loadMoreAncestors()

        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-before", CommentFixture.a])
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.myVote, .dislike)
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.votesCountUp, 7)
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.votesCountDown, 1)
        let voted = viewModel.ancestorPagination.items.first { $0.id == CommentFixture.a }
        XCTAssertEqual(voted?.election?.myVote, .dislike)
    }

    func testFailedAncestorVoteRestoresSidecarElectionAcrossPrepend() async throws {
        let rootPath = "/api/v1/posts/\(CommentFixture.root)"
        let ancestorsPath = "/api/v1/posts/\(CommentFixture.b)/ancestors"
        CannedFeedURLProtocol.handlers[rootPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["\(rootPath)/descendants"] = try (
            descendantsPage(ids: [CommentFixture.b], cursor: nil, hasMore: false), 200
        )
        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = try [
            (ancestorsPage(
                ids: [CommentFixture.root, CommentFixture.a, CommentFixture.b],
                cursor: "earlier-ancestors", hasMore: true
            ), 200, 0),
            (ancestorsPage(
                ids: [CommentFixture.root, "comment-before"],
                cursor: nil, hasMore: false, extraPostId: "comment-before"
            ), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/\(CommentFixture.a)/vote"] = (
            Data(#"{"message":"vote failed"}"#.utf8), 503
        )
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(), rootPostId: CommentFixture.root, currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        XCTAssertNil(viewModel.ancestorPosts.first?.election)
        XCTAssertEqual(viewModel.voteChoicesByPostId[CommentFixture.a], .like)
        await viewModel.vote(postId: CommentFixture.a, choice: .dislike)

        XCTAssertEqual(viewModel.voteChoicesByPostId[CommentFixture.a], .like)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.myVote, .like)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.votesCountUp, 8)
        XCTAssertEqual(viewModel.ancestorPosts.first?.election?.votesCountDown, 0)
        XCTAssertEqual(viewModel.postElectionsById[CommentFixture.a]?.votesCountUp, 8)

        await viewModel.loadMoreAncestors()
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-before", CommentFixture.a])
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.myVote, .like)
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.votesCountUp, 8)
        XCTAssertEqual(viewModel.ancestorPosts.last?.election?.votesCountDown, 0)
        XCTAssertEqual(viewModel.ancestorPagination.items.first { $0.id == CommentFixture.a }?.election?.myVote, .like)
    }

    func testAncestorContinuationPrependsUniqueRowsAndRetriesSameCursor() async throws {
        let detailPath = "/api/v1/posts/\(CommentFixture.root)"
        let ancestorsPath = "/api/v1/posts/\(CommentFixture.b)/ancestors"
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        CannedFeedURLProtocol.handlers["\(detailPath)/descendants"] = (
            ApiFixtureLoader.data("native.comments.descendants.default"),
            200
        )
        let initialPage = try ancestorsPage(
            ids: [CommentFixture.root, CommentFixture.a, CommentFixture.b],
            cursor: "ancestor-cursor",
            hasMore: true
        )
        let terminalPage = try ancestorsPage(
            ids: [CommentFixture.root, "comment-before", CommentFixture.a],
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
            rootPostId: CommentFixture.root,
            currentUserId: "user-1"
        )

        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        if case let .error(error) = viewModel.ancestorState {
            XCTFail("Initial ancestor page failed: \(error.localizedDescription)")
        }
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), [CommentFixture.a])
        XCTAssertTrue(viewModel.ancestorPagination.hasMore)

        await viewModel.loadMoreAncestors()
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), [CommentFixture.a])
        XCTAssertNotNil(viewModel.ancestorPagination.lastError)

        await viewModel.loadMoreAncestors()
        XCTAssertEqual(
            viewModel.ancestorPosts.map(\.id),
            ["comment-before", CommentFixture.a]
        )
        XCTAssertNotNil(viewModel.postMetricsById["comment-before"])
        XCTAssertNil(viewModel.ancestorPagination.lastError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == ancestorsPath }.map(\.query), [
            "limit=5",
            "limit=5&after=ancestor-cursor",
            "limit=5&after=ancestor-cursor"
        ])

        CannedFeedURLProtocol.queuedHandlers[ancestorsPath] = [(initialPage, 200, 0)]
        await viewModel.loadPermalink(targetCommentId: CommentFixture.b)
        XCTAssertNil(viewModel.postMetricsById["comment-before"])
    }

    func testDescendantFailurePreservesPageThenRetryAppendsDeduplicatedRows() async throws {
        let detailPath = "/api/v1/posts/\(CommentFixture.root)"
        let descendantsPath = "\(detailPath)/descendants"
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.comments.post-detail.default"),
            200
        )
        let initialPage = try descendantsPage(
            ids: [CommentFixture.a, CommentFixture.b],
            cursor: "descendants-cursor",
            hasMore: true
        )
        let terminalPage = try descendantsPage(
            ids: [CommentFixture.b, CommentFixture.c],
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
            rootPostId: CommentFixture.root,
            currentUserId: "user-1"
        )

        await viewModel.loadThread()
        await viewModel.loadMoreDescendants()

        XCTAssertEqual(viewModel.descendantPosts.map(\.id), [CommentFixture.a, CommentFixture.b])
        XCTAssertNotNil(viewModel.descendantPagination.lastError)

        await viewModel.loadMoreDescendants()

        XCTAssertEqual(viewModel.descendantPosts.map(\.id), [CommentFixture.a, CommentFixture.b, CommentFixture.c])
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
            var post = try XCTUnwrap(posts[CommentFixture.a] as? [String: Any])
            post["id"] = extraPostId
            posts[extraPostId] = post
            var metrics = try XCTUnwrap(object["posts_metrics"] as? [String: Any])
            var postMetrics = try XCTUnwrap(metrics[CommentFixture.a] as? [String: Any])
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
