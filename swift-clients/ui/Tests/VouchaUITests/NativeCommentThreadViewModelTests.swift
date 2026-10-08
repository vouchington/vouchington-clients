// swiftlint:disable file_length
import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeCommentThreadViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testBuildTreeSortsByNewAndBest() {
        let now = Date(timeIntervalSince1970: 10_000)
        let rootA = makePost(id: "comment-a", createdAt: now.addingTimeInterval(-120), score: 1)
        let rootB = makePost(id: "comment-b", createdAt: now.addingTimeInterval(-60), score: 10)
        let replyA = makePost(
            id: "comment-a-1",
            parentId: "comment-a",
            createdAt: now.addingTimeInterval(-30),
            score: 3
        )
        let replyB = makePost(
            id: "comment-b-1",
            parentId: "comment-b",
            createdAt: now.addingTimeInterval(-15),
            score: 0
        )

        let newTree = NativeCommentThreadViewModel.buildTree(posts: [replyB, rootA, replyA, rootB], sort: .new)
        XCTAssertEqual(newTree.map(\.post.id), ["comment-b", "comment-a"])
        XCTAssertEqual(newTree.first?.children.map(\.post.id), ["comment-b-1"])
        XCTAssertEqual(newTree.last?.children.map(\.post.id), ["comment-a-1"])

        let bestTree = NativeCommentThreadViewModel.buildTree(posts: [replyB, rootA, replyA, rootB], sort: .best)
        XCTAssertEqual(bestTree.map(\.post.id), ["comment-b", "comment-a"])
        XCTAssertEqual(bestTree.first?.children.map(\.post.id), ["comment-b-1"])
        XCTAssertEqual(bestTree.last?.children.map(\.post.id), ["comment-a-1"])
    }

    func testCollapseStorageKeyIncludesOptionalUserId() {
        XCTAssertEqual(
            NativeCommentThreadViewModel.collapseStorageKey(rootPostId: "root-1"),
            "comments-collapsed:root-1"
        )
        XCTAssertEqual(
            NativeCommentThreadViewModel.collapseStorageKey(rootPostId: "root-1", userId: "user-1"),
            "comments-collapsed:root-1:user-1"
        )
    }

    func testLoadPermalinkTracksStatesAndBuildsTree() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (rootDetailJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1/descendants"] = (descendantsJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-b/ancestors"] = (ancestorsJSON, 200)

        let viewModel = try makeViewModel()
        let task = Task { await viewModel.loadPermalink(targetCommentId: "comment-b") }
        await Task.yield()

        XCTAssertTrue(viewModel.focusedCommentId == "comment-b")
        XCTAssertTrue(isLoading(viewModel.ancestorState))
        XCTAssertTrue(isLoading(viewModel.focusedState))
        XCTAssertTrue(isLoading(viewModel.threadState))

        await task.value

        XCTAssertEqual(viewModel.rootPost?.id, "root-1")
        XCTAssertEqual(viewModel.focusedCommentId, "comment-b")
        XCTAssertEqual(viewModel.ancestorPosts.map(\.id), ["comment-a"])
        XCTAssertEqual(viewModel.commentTree.map(\.post.id), ["comment-a"])
        XCTAssertEqual(viewModel.commentTree.first?.children.map(\.post.id), ["comment-b"])
        XCTAssertTrue(isLoaded(viewModel.ancestorState))
        XCTAssertTrue(isLoaded(viewModel.focusedState))
        XCTAssertTrue(isLoaded(viewModel.threadState))
    }

    func testBlankPermalinkTargetLoadsNormalThread() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (rootDetailJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1/descendants"] = (descendantsJSON, 200)

        let viewModel = try makeViewModel()
        await viewModel.loadPermalink(targetCommentId: "  ")

        XCTAssertNil(viewModel.focusedCommentId)
        XCTAssertEqual(viewModel.rootPost?.id, "root-1")
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.allSatisfy { !$0.path.contains("/ancestors") })
        XCTAssertTrue(isLoaded(viewModel.threadState))
        XCTAssertTrue(isLoaded(viewModel.ancestorState) == false)
        XCTAssertTrue(isLoaded(viewModel.focusedState) == false)
    }

    func testMutationHelpersTargetExpectedEndpointsAndAvoidRedundantReloads() async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/posts/root-1": (rootDetailJSON, 200),
            "/api/v1/posts/root-1/descendants": (descendantsJSON, 200),
            "/api/v1/posts/comment-a/vote": (emptyJSON, 200),
            "/api/v1/bookmarks/post/comment-a/save": (emptyJSON, 200),
            "/api/v1/bookmarks/post/comment-b/save": (emptyJSON, 200),
            "/api/v1/reports": (emptyJSON, 200),
            "/api/v1/posts/comment-a": (mutationPostJSON, 200),
            "/api/v1/posts/comment-a/lock": (emptyJSON, 200),
            "/api/v1/posts": (mutationPostJSON, 200)
        ]

        let viewModel = try makeViewModel()
        await viewModel.loadThread()
        await viewModel.vote(postId: "comment-a", choice: .dislike)
        XCTAssertEqual(viewModel.descendantPosts.first { $0.id == "comment-a" }?.election?.votesCountUp, 2)
        XCTAssertEqual(viewModel.descendantPosts.first { $0.id == "comment-a" }?.election?.votesCountDown, 1)
        await viewModel.toggleSave(postId: "comment-b")
        await viewModel.toggleSave(postId: "comment-a")
        await viewModel.report(postId: "comment-a", reason: "spam", note: "details", turnstileToken: "report-token")
        await viewModel.edit(postId: "comment-a", markdown: "Edited")
        let editedComment = try XCTUnwrap(viewModel.descendantPosts.first { $0.id == "comment-a" })
        XCTAssertEqual(editedComment.markdown, "Updated")
        XCTAssertEqual(editedComment.canEditContent, true)
        XCTAssertEqual(editedComment.canDelete, true)
        await viewModel.delete(postId: "comment-a")
        await viewModel.toggleLock(postId: "comment-a", lockedAt: nil)
        await viewModel.toggleLock(postId: "comment-a", lockedAt: Date(timeIntervalSince1970: 1))
        await viewModel.reply(
            parentId: "comment-a",
            markdown: "  Reply body  ",
            isAnonymous: true,
            turnstileToken: "token"
        )
        await viewModel.reply(parentId: "comment-a", markdown: "   ")

        XCTAssertEqual(viewModel.voteChoicesByPostId["comment-a"], .like)
        XCTAssertTrue(isLoaded(viewModel.mutationState))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedMethods.filter { $0 != "GET" },
            ["PUT", "PUT", "DELETE", "POST", "PATCH", "DELETE", "POST", "DELETE", "POST"]
        )
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/reports" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/bookmarks/post/comment-b/save" })
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "GET" }.count, 8)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains { $0.contains("report-token") })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains { $0.contains("Reply body") })
    }

    func testMutationErrorsUpdateState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-a/vote"] = (emptyJSON, 500)

        let viewModel = try makeViewModel()
        await viewModel.vote(postId: "comment-a", choice: .like)

        if case .error = viewModel.mutationState {
            XCTAssertTrue(true)
        } else {
            XCTFail("Expected mutation error state")
        }
    }

    func testVoteIgnoresConcurrentRequestAndRestoresOriginalStateAfterFailure() async throws {
        let votePath = "/api/v1/posts/comment-a/vote"
        CannedFeedURLProtocol.handlers = [
            "/api/v1/posts/root-1": (rootDetailJSON, 200),
            "/api/v1/posts/root-1/descendants": (descendantsJSON, 200),
            votePath: (emptyJSON, 500)
        ]
        let viewModel = try makeViewModel()
        await viewModel.loadThread()
        let originalChoice = viewModel.voteChoicesByPostId["comment-a"]
        let originalElection = viewModel.descendantPosts.first { $0.id == "comment-a" }?.election
        CannedFeedURLProtocol.suspendResponse(path: votePath)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: votePath, method: "PUT")

        let first = Task { await viewModel.vote(postId: "comment-a", choice: .dislike) }
        _ = try await barrier.wait()
        XCTAssertTrue(viewModel.inFlightVotePostIds.contains("comment-a"))
        await viewModel.vote(postId: "comment-a", choice: .disavow)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(votePath), 1)
        CannedFeedURLProtocol.releaseResponse(path: votePath)
        await first.value

        XCTAssertFalse(viewModel.inFlightVotePostIds.contains("comment-a"))
        XCTAssertEqual(viewModel.voteChoicesByPostId["comment-a"], originalChoice)
        let restoredElection = viewModel.descendantPosts.first { $0.id == "comment-a" }?.election
        XCTAssertEqual(restoredElection?.votesCountUp, originalElection?.votesCountUp)
        XCTAssertEqual(restoredElection?.votesCountDown, originalElection?.votesCountDown)
        XCTAssertEqual(restoredElection?.myVote, originalChoice ?? originalElection?.myVote)
        XCTAssertEqual(viewModel.postElectionsById["comment-a"]?.votesCountUp, originalElection?.votesCountUp)
        XCTAssertEqual(viewModel.postElectionsById["comment-a"]?.votesCountDown, originalElection?.votesCountDown)
    }

    func testReplyAppendUsesLocalPermissionsWhenMutationOmitsPermissionFields() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (rootDetailJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1/descendants"] = (descendantsJSON, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [(newReplyMutationPostJSON, 200, 0)]

        let viewModel = try makeViewModel()
        await viewModel.loadThread()
        await viewModel.reply(parentId: "comment-a", markdown: "Fresh reply")

        let reply = try XCTUnwrap(viewModel.descendantPosts.first { $0.id == "comment-c" })
        XCTAssertEqual(reply.canEditContent, true)
        XCTAssertEqual(reply.canDelete, true)
        XCTAssertEqual(reply.canLock, true)
    }

    func testEditUpdatesPermalinkAncestorRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (rootDetailJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1/descendants"] = (descendantsJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-b/ancestors"] = (ancestorsJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-a"] = (mutationPostJSON, 200)

        let viewModel = try makeViewModel()
        await viewModel.loadPermalink(targetCommentId: "comment-b")
        await viewModel.edit(postId: "comment-a", markdown: "Edited")

        let ancestor = try XCTUnwrap(viewModel.ancestorPosts.first { $0.id == "comment-a" })
        XCTAssertEqual(ancestor.markdown, "Updated")
        XCTAssertEqual(ancestor.canEditContent, true)
        XCTAssertEqual(viewModel.commentTree.first?.post.markdown, "Updated")
    }

    func testRootEditLoadedBySlugMergesCanonicalPostId() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-slug"] = (rootDetailJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-slug/descendants"] = (descendantsJSON, 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (rootMutationPostJSON, 200)

        let viewModel = try makeViewModel(rootPostId: "root-slug")
        await viewModel.loadThread()
        await viewModel.edit(postId: "root-1", markdown: "Edited root")

        XCTAssertEqual(viewModel.rootPost?.markdown, "Updated root")
        XCTAssertFalse(viewModel.descendantPosts.contains { $0.id == "root-1" })
    }

    private func makeViewModel(rootPostId: String = "root-1") throws -> NativeCommentThreadViewModel {
        let baseURL = try XCTUnwrap(URL(string: "http://localhost:2999"))
        return NativeCommentThreadViewModel(
            client: APIClient(
                config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
                cookieStorage: HTTPCookieStorage(),
                protocolClasses: [CannedFeedURLProtocol.self]
            ),
            rootPostId: rootPostId,
            currentUserId: "user-1"
        )
    }

    private func makePost(
        id: String,
        parentId: String? = nil,
        createdAt: Date,
        score: Int
    ) -> Post {
        Post(
            id: id,
            slug: nil,
            postType: .comment,
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
            election: .init(votesScoreNet: Double(score), votesCountUp: score, votesCountDown: 0)
        )
    }

    private var emptyJSON: Data {
        Data("{}".utf8)
    }

    private var mutationPostJSON: Data {
        Data("""
        {
          "post": {
            "id": "comment-a",
            "slug": null,
            "post_type": "comment",
            "title": null,
            "markdown": "Updated",
            "html": "<p>Updated</p>",
            "parent_post_id": "root-1",
            "root_post_id": "root-1",
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:01:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }

    private var newReplyMutationPostJSON: Data {
        Data("""
        {
          "post": {
            "id": "comment-c",
            "slug": null,
            "post_type": "comment",
            "title": null,
            "markdown": "Fresh reply",
            "html": "<p>Fresh reply</p>",
            "parent_post_id": "comment-a",
            "root_post_id": "root-1",
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:03:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }

    private var rootMutationPostJSON: Data {
        Data("""
        {
          "post": {
            "id": "root-1",
            "slug": "root-1",
            "post_type": "discussion",
            "title": "Root",
            "markdown": "Updated root",
            "html": "<p>Updated root</p>",
            "parent_post_id": null,
            "root_post_id": null,
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }

    private var rootDetailJSON: Data {
        Data("""
        {
          "post": {
            "id": "root-1",
            "slug": "root-1",
            "post_type": "discussion",
            "title": "Root",
            "markdown": "Root body",
            "html": "<p>Root body</p>",
            "parent_post_id": null,
            "root_post_id": null,
            "created_by_id": "user-1",
            "created_by": {
              "id": "user-1",
              "username": "alice",
              "roles": [],
              "profile_image_id": null
            },
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null,
            "can_edit_content": true,
            "can_delete": true,
            "can_lock": true
          },
          "html": "<p>Root body</p>",
          "post_metrics": { "count": { "descendants": 2, "children": 1, "ancestors": 0 } },
          "post_election": { "votes_score_net": 0, "votes_count_up": 0, "votes_count_down": 0 },
          "election_vote": null,
          "bookmarks": { "root-1": { "save": false } }
        }
        """.utf8)
    }

    private var descendantsJSON: Data {
        Data("""
        {
          "results": [
            { "entity_id": "comment-a" },
            { "entity_id": "comment-b" }
          ],
          "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
          "posts": {
            "comment-a": {
              "id": "comment-a",
              "slug": null,
              "post_type": "comment",
              "title": null,
              "markdown": "First reply",
              "html": "<p>First reply</p>",
              "parent_post_id": "root-1",
              "root_post_id": "root-1",
              "created_by_id": "user-1",
              "created_at": "2026-01-01T00:01:00Z",
              "broadcast": "everyone",
              "privacy": "public",
              "is_anonymous": false,
              "community_id": null,
              "clearance_status": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "locked_at": null,
              "locked_by_id": null,
              "archived_at": null,
              "archived_by_id": null,
              "clearance_reason": null,
              "clearance_updated_at": null,
              "spam_detection_created_at": null,
              "spam_detection_flagged": null,
              "spam_detection_results": null,
              "spam_detection_score": null,
              "updated_by_id": null,
              "can_edit_content": true,
              "can_delete": true,
              "can_lock": true,
              "election": {
                "votes_score_net": 3,
                "votes_count_up": 3,
                "votes_count_down": 0
              }
            },
            "comment-b": {
              "id": "comment-b",
              "slug": null,
              "post_type": "comment",
              "title": null,
              "markdown": "Nested reply",
              "html": "<p>Nested reply</p>",
              "parent_post_id": "comment-a",
              "root_post_id": "root-1",
              "created_by_id": "user-2",
              "created_by": {
                "id": "user-2",
                "username": "bob",
                "roles": [],
                "profile_image_id": null
              },
              "created_at": "2026-01-01T00:02:00Z",
              "broadcast": "everyone",
              "privacy": "public",
              "is_anonymous": false,
              "community_id": null,
              "clearance_status": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "locked_at": null,
              "locked_by_id": null,
              "archived_at": null,
              "archived_by_id": null,
              "clearance_reason": null,
              "clearance_updated_at": null,
              "spam_detection_created_at": null,
              "spam_detection_flagged": null,
              "spam_detection_results": null,
              "spam_detection_score": null,
              "updated_by_id": null,
              "can_edit_content": false,
              "can_delete": false,
              "can_lock": false,
              "election": {
                "votes_score_net": 6,
                "votes_count_up": 6,
                "votes_count_down": 0
              }
            }
          },
          "posts_metrics": {
            "comment-a": { "count": { "descendants": 1, "children": 1, "ancestors": 1 } },
            "comment-b": { "count": { "descendants": 0, "children": 0, "ancestors": 2 } }
          },
          "post_elections": {
            "comment-a": { "votes_score_net": 3, "votes_count_up": 3, "votes_count_down": 0 },
            "comment-b": { "votes_score_net": 6, "votes_count_up": 6, "votes_count_down": 0 }
          },
          "election_votes": {
            "comment-a": {
              "__entity_type": "election_vote",
              "entity_id": "comment-a",
              "user_id": "user-1",
              "choice": "like",
              "created_at": "2026-01-01T00:00:00Z"
            },
            "comment-b": {
              "__entity_type": "election_vote",
              "entity_id": "comment-b",
              "user_id": "user-1",
              "choice": "dislike",
              "created_at": "2026-01-01T00:00:00Z"
            }
          },
          "markdown_to_html": {
            "comment-a": "<p>First reply</p>",
            "comment-b": "<p>Nested reply</p>"
          },
          "bookmarks": {
            "comment-a": { "save": true },
            "comment-b": { "save": false }
          }
        }
        """.utf8)
    }

    private var ancestorsJSON: Data {
        Data("""
        {
          "results": [
            { "entity_id": "comment-a" }
          ],
          "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
          "posts": {
            "comment-a": {
              "id": "comment-a",
              "slug": null,
              "post_type": "comment",
              "title": null,
              "markdown": "First reply",
              "html": "<p>First reply</p>",
              "parent_post_id": "root-1",
              "root_post_id": "root-1",
              "created_by_id": "user-1",
              "created_at": "2026-01-01T00:01:00Z",
              "broadcast": "everyone",
              "privacy": "public",
              "is_anonymous": false,
              "community_id": null,
              "clearance_status": null,
              "deleted_at": null,
              "deleted_by_id": null,
              "locked_at": null,
              "locked_by_id": null,
              "archived_at": null,
              "archived_by_id": null,
              "clearance_reason": null,
              "clearance_updated_at": null,
              "spam_detection_created_at": null,
              "spam_detection_flagged": null,
              "spam_detection_results": null,
              "spam_detection_score": null,
              "updated_by_id": null,
              "can_edit_content": true,
              "can_delete": true,
              "can_lock": true
            }
          },
          "posts_metrics": {
            "comment-a": { "count": { "descendants": 1, "children": 1, "ancestors": 1 } }
          },
          "markdown_to_html": {
            "comment-a": "<p>First reply</p>"
          }
        }
        """.utf8)
    }

    private func isLoading(_ state: LoadState) -> Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    private func isLoaded(_ state: LoadState) -> Bool {
        guard case .loaded = state else { return false }
        return true
    }
}
