import Foundation
import ViewInspector
import VouchaAPI
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadComponentTests: XCTestCase {
    func testCommentThreadComponentsRenderAndTapActions() throws {
        var actions: [String] = []
        let post = makePost(
            id: "comment-a",
            createdAt: Date(timeIntervalSince1970: 10),
            score: 3
        )
        let section = try CommentThreadPostSection(
            title: .verbatim("Alice"),
            post: post,
            embed: makeEmbed(),
            pathText: "/discussion/root-1/comment/comment-a",
            voteChoice: .like,
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            hideDownCount: false,
            isRoot: false,
            isCollapsed: false,
            onToggleCollapse: { actions.append("collapse") },
            onVote: { _ in actions.append("vote") },
            onReply: { actions.append("reply") },
            onQuote: { actions.append("quote") },
            onSave: { actions.append("save") },
            onReport: { actions.append("report") },
            onEdit: { actions.append("edit") },
            onDelete: { actions.append("delete") },
            onLock: { actions.append("lock") },
            isSaved: true
        )

        XCTAssertEqual(try section.inspect().find(text: "Alice").string(), "Alice")
        XCTAssertEqual(
            try section.inspect().find(text: "/discussion/root-1/comment/comment-a").string(),
            "/discussion/root-1/comment/comment-a"
        )
        XCTAssertEqual(try section.inspect().find(text: "comment-a body").string(), "comment-a body")
        XCTAssertNoThrow(try section.inspect().find(text: "Embedded post preview"))
        for title in ["Reply", "Quote", "Saved", "Report", "Edit", "Delete", "Lock"] {
            try section.inspect().find(button: title).tap()
        }

        XCTAssertEqual(actions, ["reply", "quote", "save", "report", "edit", "delete", "lock"])
    }

    private func makeEmbed() throws -> UrlEmbed {
        try JSONDecoder.vouchaFixtureDecoder.decode(UrlEmbed.self, from: Data(#"""
        { "source_url": "https://example.com/source", "title": "Embedded post preview" }
        """#.utf8))
    }

    func testCommentThreadPostSectionHidesRestrictedNegativeVoteCount() throws {
        let post = makePost(
            id: "comment-a",
            createdAt: Date(timeIntervalSince1970: 10),
            score: 4,
            downCount: 1
        )
        let section = CommentThreadPostSection(
            title: .verbatim("Alice"),
            post: post,
            embed: nil,
            pathText: "/discussion/root-1/comment/comment-a",
            voteChoice: nil,
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            hideDownCount: true,
            isRoot: false,
            isCollapsed: false,
            onToggleCollapse: {},
            onVote: { _ in },
            onReply: {},
            onQuote: {},
            onSave: {},
            onReport: nil,
            onEdit: nil,
            onDelete: nil,
            onLock: nil,
            isSaved: false
        )

        XCTAssertNoThrow(try section.inspect().find(text: "4"))
        XCTAssertThrowsError(try section.inspect().find(text: "1"))
    }

    func testCommentThreadNodeViewRendersChildrenAndPermissionActions() throws {
        var toggled: [String] = []
        let child = NativeCommentThreadNode(
            post: makePost(id: "child", parentId: "comment-a", createdAt: Date(), score: 0)
        )
        let node = NativeCommentThreadNode(
            post: makePost(id: "comment-a", createdAt: Date(), score: 1),
            children: [child]
        )
        let sut = NativeCommentThreadNodeView(
            node: node,
            rootPostId: "root-1",
            rootPostType: "discussion",
            depth: 0,
            collapsedIds: [],
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            currentUserId: "user-2",
            hideDownCount: false,
            inFlightVotePostIds: [],
            bookmarksByPostId: ["comment-a": ["save": false]],
            postEmbedsByPostId: [:],
            voteChoiceByPostId: ["comment-a": .like],
            onToggleCollapse: { toggled.append($0) },
            onVote: { _, _ in },
            onReply: { _ in },
            onQuote: { _ in },
            onSave: { _ in },
            onReport: { _ in },
            onEdit: { _ in },
            onDelete: { _ in },
            onLock: { _ in }
        )

        XCTAssertNoThrow(try sut.inspect().find(text: "/discussion/root-1/comment/comment-a"))
        XCTAssertNoThrow(try sut.inspect().find(text: "/discussion/root-1/comment/child"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Report"))
        try sut.inspect().findAll(ViewType.Button.self).first?.tap()
        XCTAssertEqual(toggled, ["comment-a"])
    }

    func testCommentNodeDisablesVoteControlsWhileItsVoteIsInFlight() throws {
        let node = NativeCommentThreadNode(
            post: makePost(id: "comment-a", createdAt: Date(), score: 1)
        )
        let sut = NativeCommentThreadNodeView(
            node: node,
            rootPostId: "root-1",
            rootPostType: "discussion",
            depth: 0,
            collapsedIds: [],
            isSignedIn: true,
            canCreateVote: true,
            showSignIn: {},
            currentUserId: "user-2",
            hideDownCount: false,
            inFlightVotePostIds: ["comment-a"],
            bookmarksByPostId: [:],
            postEmbedsByPostId: [:],
            voteChoiceByPostId: [:],
            onToggleCollapse: { _ in },
            onVote: { _, _ in },
            onReply: { _ in },
            onQuote: { _ in },
            onSave: { _ in },
            onReport: { _ in },
            onEdit: { _ in },
            onDelete: { _ in },
            onLock: { _ in }
        )

        XCTAssertFalse(try sut.inspect().find(VoteControls.self).actualView().canCreateVote)
        XCTAssertThrowsError(try sut.inspect().find(button: "Sign In"))
    }

    func testSignedOutCommentVoteRequestsSignIn() throws {
        var signInCount = 0
        let section = CommentThreadPostSection(
            title: .verbatim("Alice"),
            post: makePost(id: "comment-a", createdAt: Date(), score: 1),
            embed: nil,
            pathText: "/discussion/root-1/comment/comment-a",
            voteChoice: nil,
            isSignedIn: false,
            canCreateVote: false,
            showSignIn: { signInCount += 1 },
            hideDownCount: false,
            isRoot: false,
            isCollapsed: false,
            onToggleCollapse: {},
            onVote: { _ in },
            onReply: {},
            onQuote: {},
            onSave: {},
            onReport: nil,
            onEdit: nil,
            onDelete: nil,
            onLock: nil,
            isSaved: false
        )

        try section.inspect().find(button: "Sign In").tap()

        XCTAssertEqual(signInCount, 1)
    }

    func testComposerSheetRendersReplyReportDeleteAndTurnstileStates() throws {
        let reply = NativeCommentThreadComposerSheet(
            composer: .init(kind: .reply(parentId: "comment-a"), markdown: "Reply"),
            showingTurnstile: .constant(false),
            turnstileSiteKey: "site-key",
            onSubmit: { _ in },
            onCancel: {},
            onCaptureTurnstile: { _ in }
        )
        XCTAssertNoThrow(try reply.inspect().find(ViewType.TextEditor.self))
        XCTAssertNoThrow(try reply.inspect().find(button: "Verify"))
        XCTAssertNoThrow(try reply.inspect().find(button: "Reply"))

        let report = NativeCommentThreadComposerSheet(
            composer: .init(kind: .report(postId: "comment-a"), markdown: ""),
            showingTurnstile: .constant(false),
            turnstileSiteKey: "site-key",
            onSubmit: { _ in },
            onCancel: {},
            onCaptureTurnstile: { _ in }
        )
        XCTAssertNoThrow(try report.inspect().find(ViewType.Picker.self))
        XCTAssertNoThrow(try report.inspect().find(ViewType.TextField.self))

        let delete = NativeCommentThreadComposerSheet(
            composer: .init(kind: .delete(postId: "comment-a"), markdown: ""),
            showingTurnstile: .constant(false),
            turnstileSiteKey: "site-key",
            onSubmit: { _ in },
            onCancel: {},
            onCaptureTurnstile: { _ in }
        )
        XCTAssertNoThrow(try delete.inspect().find(button: "Delete"))
    }

    func testComposerSheetRendersRetainedContributionAdmissionFailure() throws {
        let surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: routeMatch(),
            isSignedIn: true,
            showSignIn: {}
        )
        let sheet = surface.composerSheet(
            for: .init(kind: .reply(parentId: "comment-a"), markdown: "Reply")
        )
        let message = UiMessages.string(
            .nativeTaxonomyContributionAdmissionCapacityUnavailable,
            locale: .init(identifier: "en")
        )

        XCTAssertThrowsError(try sheet.inspect().find(text: message))

        surface.viewModel.mutationState = .error(
            .api(statusCode: 429, preconditionCode: "CONTRIBUTION_QUOTA_EXCEEDED")
        )

        XCTAssertNoThrow(try sheet.inspect().find(text: message))
    }

    func testSurfaceHelperBranches() {
        var signInCount = 0
        let surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: nil,
            currentUserId: nil,
            isSignedIn: false,
            showSignIn: { signInCount += 1 }
        )
        let post = makePost(id: "comment-a", createdAt: Date(), score: 1)

        XCTAssertEqual(surface.loadTaskId, "||")
        XCTAssertEqual(surface.authorName(for: post), .verbatim("user-1"))
        XCTAssertEqual(surface.threadRootType(post), "comment")
        XCTAssertNil(surface.noteOrNil("  "))
        XCTAssertEqual(surface.noteOrNil(" note "), "note")
        XCTAssertTrue(surface.quoteMarkdown(post).contains("> comment-a body"))
        XCTAssertFalse(surface.canReport(post: post))

        surface.handleVote(postId: post.id, choice: .like)
        surface.handleSave(postId: post.id)
        surface.handleLock(post: post)
        surface.startReply(post: post, quote: false)
        surface.startReply(postId: post.id, quote: true)
        surface.startEdit(post: post)
        surface.startReport(post: post)
        surface.startDelete(post: post)
        XCTAssertEqual(signInCount, 8)
    }

    func testSignedInSurfaceHelpersSetComposerAndSubmitBranches() async {
        var signInCount = 0
        let surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: routeMatch(),
            currentUserId: "me",
            isSignedIn: true,
            showSignIn: { signInCount += 1 }
        )
        let post = makePost(id: "comment-a", createdAt: Date(), score: 1)

        XCTAssertTrue(surface.canReport(post: post))

        surface.startReply(post: post, quote: false)
        surface.startReply(post: post, quote: true)
        surface.startReply(postId: post.id, quote: true)
        surface.startQuote(post: post)
        surface.startEdit(post: post)
        surface.startReport(post: post)
        surface.startDelete(post: post)

        await surface.submitComposer(.init(kind: .reply(parentId: post.id), markdown: "Reply"))
        await surface.submitComposer(.init(kind: .edit(postId: post.id), markdown: "Edit"))
        await surface.submitComposer(.init(kind: .report(postId: post.id), markdown: "", note: " note "))
        await surface.submitComposer(.init(kind: .delete(postId: post.id), markdown: ""))
        XCTAssertEqual(signInCount, 0)
    }

    func testSurfaceRendersLoadedLoadingAndErrorBranches() throws {
        let root = makePost(id: "root-1", createdAt: Date(), score: 3)
        let ancestor = makePost(id: "ancestor", parentId: "root-1", createdAt: Date(), score: 2)
        let child = makePost(id: "comment-a", parentId: "ancestor", createdAt: Date(), score: 1)
        let loaded = seededSurface(root: root, ancestor: ancestor, child: child)

        XCTAssertNoThrow(try loaded.inspect().find(ViewType.Picker.self))
        XCTAssertNoThrow(try loaded.inspect().find(text: "/comment/root-1"))
        XCTAssertNoThrow(try loaded.inspect().find(text: "Ancestor chain"))
        XCTAssertNoThrow(try loaded.inspect().find(text: "/comment/root-1/comment/ancestor"))
        XCTAssertNoThrow(try loaded.inspect().find(text: "Comments"))
        XCTAssertNoThrow(try loaded.inspect().find(text: "/comment/root-1/comment/comment-a"))

        loaded.viewModel.ancestorPagination.reset(items: [root])
        loaded.viewModel.ancestorPagination.restoreContinuation(endCursor: "ancestor-cursor", hasMore: true)
        XCTAssertNoThrow(try loaded.inspect().find(viewWithAccessibilityIdentifier: "comment-ancestors-pagination"))

        let empty = seededSurface(root: root, ancestor: nil, child: nil)
        XCTAssertNoThrow(try empty.inspect().find(text: "No comments yet"))

        let loading = stateSurface(threadState: .loading)
        XCTAssertNoThrow(try loading.inspect().find(ViewType.ProgressView.self))

        let errored = stateSurface(threadState: .error(.api(statusCode: 500, preconditionCode: nil)))
        XCTAssertNoThrow(try errored.inspect().find(text: "Unable to load thread"))
    }

    func testSurfaceShowsFollowerDistributionForEligibleRootPost() throws {
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let client = try APIClient(
            config: .init(baseURL: baseURL, turnstileSiteKey: "test"),
            cookieStorage: .init(),
            protocolClasses: [FailingURLProtocol.self]
        )
        let surface = NativeCommentThreadSurface(
            client: client,
            routeMatch: routeMatch(),
            currentUserId: "viewer-1",
            isSignedIn: true,
            showSignIn: {}
        )
        surface.viewModel.rootPost = makePost(
            id: "root-1",
            createdAt: .now,
            score: 1,
            postType: .discussion
        )

        XCTAssertNoThrow(try surface.inspect().find(FollowerDistributionActions.self))
    }

    func testComposerSheetRendersEditAndDisabledStates() throws {
        let edit = NativeCommentThreadComposerSheet(
            composer: .init(kind: .edit(postId: "comment-a"), markdown: ""),
            showingTurnstile: .constant(false),
            turnstileSiteKey: "site-key",
            onSubmit: { _ in },
            onCancel: {},
            onCaptureTurnstile: { _ in }
        )

        XCTAssertNoThrow(try edit.inspect().find(ViewType.TextEditor.self))
        XCTAssertThrowsError(try edit.inspect().find(button: "Verify"))
        XCTAssertTrue(try edit.inspect().find(button: "Save").isDisabled())
    }

    func testViewModelStateHelpersWithoutClient() async {
        let viewModel = NativeCommentThreadViewModel(client: nil, rootPostId: "root-1", currentUserId: "me")

        XCTAssertEqual(viewModel.collapseStorageKey, "comments-collapsed:root-1:me")
        XCTAssertFalse(viewModel.isLoading)
        await viewModel.loadThread()
        XCTAssertTrue(isError(viewModel.threadState))
        await viewModel.loadPermalink(targetCommentId: "comment-a")
        XCTAssertTrue(isError(viewModel.threadState))
        XCTAssertTrue(isError(viewModel.ancestorState))
        XCTAssertTrue(isError(viewModel.focusedState))
        viewModel.setSort(.new)
        XCTAssertEqual(viewModel.sort, .new)
        viewModel.setSort(.best)
        XCTAssertEqual(viewModel.sort, .best)
        viewModel.toggleCollapse(commentId: "comment-a")
        XCTAssertEqual(viewModel.collapsedCommentIds, ["comment-a"])
        viewModel.toggleCollapse(commentId: "comment-a")
        XCTAssertTrue(viewModel.collapsedCommentIds.isEmpty)
    }

    private func routeMatch(commentId: String? = nil) -> NativeRouteMatch {
        var params = ["id": "root-1"]
        if let commentId {
            params["commentId"] = commentId
        }
        return NativeRouteMatch(
            path: commentId.map { "/comment/root-1/comment/\($0)" } ?? "/comment/root-1",
            template: "/comment/:id",
            params: params
        )
    }

    private func seededSurface(root: Post, ancestor: Post?, child: Post?) -> NativeCommentThreadSurface {
        let surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: routeMatch(),
            currentUserId: "me",
            isSignedIn: true,
            showSignIn: {}
        )
        surface.viewModel.rootPost = root
        surface.viewModel.ancestorPosts = ancestor.map { [$0] } ?? []
        surface.viewModel.commentTree = child.map { [NativeCommentThreadNode(post: $0)] } ?? []
        surface.viewModel.bookmarksByPostId[root.id] = ["save": true]
        surface.viewModel.voteChoicesByPostId[root.id] = .like
        if let ancestor {
            surface.viewModel.bookmarksByPostId[ancestor.id] = ["save": false]
            surface.viewModel.voteChoicesByPostId[ancestor.id] = .neutral
        }
        if let child {
            surface.viewModel.bookmarksByPostId[child.id] = ["save": false]
            surface.viewModel.voteChoicesByPostId[child.id] = .dislike
        }
        return surface
    }

    private func stateSurface(threadState: LoadState) -> NativeCommentThreadSurface {
        let surface = NativeCommentThreadSurface(
            client: nil,
            routeMatch: routeMatch(),
            currentUserId: "me",
            isSignedIn: true,
            showSignIn: {}
        )
        surface.viewModel.threadState = threadState
        return surface
    }

    private func isError(_ state: LoadState) -> Bool {
        if case .error = state {
            return true
        }
        return false
    }

    private func makePost(
        id: String,
        parentId: String? = nil,
        createdAt: Date,
        score: Int,
        downCount: Int = 0,
        postType: PostType = .comment
    ) -> Post {
        Post(
            id: id,
            slug: nil,
            postType: postType,
            title: nil,
            markdown: "\(id) body",
            html: nil,
            parentId: parentId,
            rootId: "root-1",
            createdById: "user-1",
            createdAt: createdAt,
            broadcast: .everyone,
            privacy: .public,
            isAnonymous: false,
            communityId: nil,
            clearanceStatus: nil,
            election: .init(votesScoreNet: Double(score), votesCountUp: score, votesCountDown: downCount)
        )
    }
}
