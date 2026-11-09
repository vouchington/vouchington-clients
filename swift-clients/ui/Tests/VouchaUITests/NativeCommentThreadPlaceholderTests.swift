import Foundation
import ViewInspector
import VouchaAPI
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadPlaceholderTests: XCTestCase {
    func testBothPlaceholderStatesHideAuthorContentMediaAndAllActions() throws {
        for (state, label) in [("deleted", "Deleted"), ("unmoderated", "Pending")] {
            let post = try placeholder(state)
            let section = section(post)
            XCTAssertNoThrow(try section.inspect().find(text: label))
            for content in ["Secret author", "Secret body", "Secret title", "/discussion/root/comment/a"] {
                XCTAssertThrowsError(try section.inspect().find(text: content))
            }
            for action in ["Reply", "Quote", "Save", "Report", "Edit", "Delete", "Lock"] {
                XCTAssertThrowsError(try section.inspect().find(button: action))
            }
            XCTAssertThrowsError(try section.inspect().find(PostAuthorAvatar.self))
            XCTAssertThrowsError(try section.inspect().find(ProviderEmbedPreview.self))
            XCTAssertFalse(post.canShowAuthoredMedia(isStaffViewer: true))
            XCTAssertEqual(post.withRenderedHtml("<p>Secret body</p>").placeholderState, state)
        }
    }

    func testCollapsedHeadersUseDirectChildMetricsForRegularAndPlaceholderComments() throws {
        for state in [nil, "deleted", "unmoderated"] as [String?] {
            let post = try placeholder(state)
            let model = NativeCommentThreadNode(post: post)
            let metrics = try JSONDecoder.vouchaFixtureDecoder.decode(
                PostMetrics.self,
                from: Data(#"{"count":{"children":42,"descendants":84}}"#.utf8)
            )
            let view = NativeCommentThreadNodeView(
                node: model, rootPostId: "root", rootPostType: "discussion", depth: 0,
                collapsedIds: [post.id], isSignedIn: true, canCreateVote: true,
                showSignIn: {}, currentUserId: "staff", hideDownCount: false,
                inFlightVotePostIds: [], bookmarksByPostId: [:], postEmbedsByPostId: [:],
                voteChoiceByPostId: [:], postMetricsById: [post.id: metrics],
                onToggleCollapse: { _ in }, onVote: { _, _ in }, onReply: { _ in },
                onQuote: { _ in }, onSave: { _ in }, onReport: { _ in }, onEdit: { _ in },
                onDelete: { _ in }, onLock: { _ in }
            )
            XCTAssertNoThrow(try view.inspect().find(text: "42"))
            XCTAssertThrowsError(try view.inspect().find(text: "84"))
            XCTAssertThrowsError(try view.inspect().find(text: "Secret body"))
        }
    }

    func testPlaceholderDescendantKeepsItsVisibleRepliesInTree() throws {
        let envelope = try JSONDecoder.vouchaFixtureDecoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        let deleted = try XCTUnwrap(envelope.posts["comment-d"])
        let child = try placeholder(nil, parent: deleted.id)
        let tree = NativeCommentThreadViewModel.buildTree(posts: [child, deleted])
        XCTAssertEqual(tree.map(\.id), [deleted.id])
        XCTAssertEqual(tree.first?.children.map(\.id), [child.id])
        XCTAssertEqual(tree.first?.post.placeholderState, "deleted")
    }

    func testExpandedSubtreeFailureShowsNativeRetryAction() async throws {
        let post = try placeholder("unmoderated")
        var pagination = CursorPaginationState<Post>()
        let request = try XCTUnwrap(pagination.beginNextPage())
        pagination.fail(request, error: .api(statusCode: 503, preconditionCode: nil))
        let retry = expectation(description: "Retry requested for hidden subtree")
        let view = NativeCommentThreadNodeView(
            node: NativeCommentThreadNode(post: post), rootPostId: "root", rootPostType: "discussion", depth: 0,
            collapsedIds: [], isSignedIn: true, canCreateVote: true,
            showSignIn: {}, currentUserId: "staff", hideDownCount: false,
            inFlightVotePostIds: [], bookmarksByPostId: [:], postEmbedsByPostId: [:],
            voteChoiceByPostId: [:], subtreePagination: [post.id: pagination],
            onLoadHiddenDescendants: { id in
                XCTAssertEqual(id, post.id)
                retry.fulfill()
            },
            onToggleCollapse: { _ in }, onVote: { _, _ in }, onReply: { _ in },
            onQuote: { _ in }, onSave: { _ in }, onReport: { _ in }, onEdit: { _ in },
            onDelete: { _ in }, onLock: { _ in }
        )
        try view.inspect().find(button: "Try Again").tap()
        await fulfillment(of: [retry], timeout: 2)
    }

    private func placeholder(_ state: String?, parent: String = "root") throws -> Post {
        let envelope = try JSONDecoder.vouchaFixtureDecoder.decode(
            PostThreadEnvelope.self,
            from: ApiFixtureLoader.data("native.comments.descendants.default")
        )
        let author = try XCTUnwrap(envelope.posts["comment-a"]?.createdBy)
        return Post(
            id: "unsafe-comment", slug: nil, postType: .comment, title: "Secret title",
            markdown: "Secret body", html: nil, parentId: parent, rootId: "root",
            createdById: author.id, createdAt: Date(timeIntervalSince1970: 0),
            broadcast: .everyone, privacy: .public, isAnonymous: false, communityId: nil,
            clearanceStatus: .approved, createdBy: author, canEditContent: true,
            canDelete: true, canLock: true, placeholderState: state
        )
    }

    private func section(_ post: Post) -> CommentThreadPostSection {
        CommentThreadPostSection(
            title: .verbatim("Secret author"), post: post, embed: nil,
            pathText: "/discussion/root/comment/a", voteChoice: nil,
            isSignedIn: true, canCreateVote: true, showSignIn: {}, hideDownCount: false,
            isRoot: false, isCollapsed: false, onToggleCollapse: {}, onVote: { _ in },
            onReply: {}, onQuote: {}, onSave: {}, onReport: {}, onEdit: {}, onDelete: {},
            onLock: {}, isSaved: false, currentUserId: "staff", isStaffViewer: true
        )
    }
}
