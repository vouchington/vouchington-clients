import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EmailVerificationGatedMutationTests: NativeRouteSurfaceViewModelTestCase {
    func testSuccessfulMutationReturnsTypedValueWithoutRollback() async throws {
        let gate = EmailVerificationGatedMutation()
        var rollbackCount = 0

        let value: Int = try await gate.perform(rollbackOnFailure: { rollbackCount += 1 }, { 42 })

        XCTAssertEqual(value, 42)
        XCTAssertEqual(rollbackCount, 0)
        XCTAssertFalse(gate.isRecoveryPresented)
    }

    func testVerificationFailureRollsBackOncePresentsAndRethrowsOriginalError() async {
        let gate = EmailVerificationGatedMutation()
        var rollbackCount = 0

        do {
            let _: Void = try await gate.perform(rollbackOnFailure: { rollbackCount += 1 }, {
                throw VouchaError.forbidden(preconditionCode: "EMAIL_VERIFICATION_REQUIRED")
            })
            XCTFail("Expected verification failure")
        } catch let error as VouchaError {
            XCTAssertTrue(error.isEmailVerificationRequired)
        } catch {
            XCTFail("Expected the original VouchaError")
        }

        XCTAssertEqual(rollbackCount, 1)
        XCTAssertTrue(gate.isRecoveryPresented)
    }

    func testNonVerificationFailureRollsBackWithoutClearingPendingRecovery() async {
        let gate = EmailVerificationGatedMutation()
        var rollbackCount = 0
        _ = try? await gate.perform(rollbackOnFailure: {}, {
            throw VouchaError.apiMessage(
                statusCode: 403,
                preconditionCode: "EMAIL_VERIFICATION_REQUIRED",
                message: "Verify your email"
            )
        }) as Void

        do {
            let _: Void = try await gate.perform(rollbackOnFailure: { rollbackCount += 1 }, {
                throw VouchaError.api(statusCode: 500, preconditionCode: nil)
            })
        } catch {}

        XCTAssertEqual(rollbackCount, 1)
        XCTAssertTrue(gate.isRecoveryPresented)
    }

    func testDismissRecoveryIsOneShotAndDoesNotReplayMutation() async {
        let gate = EmailVerificationGatedMutation()
        var mutationCount = 0
        _ = try? await gate.perform(rollbackOnFailure: {}, {
            mutationCount += 1
            throw VouchaError.api(statusCode: 403, preconditionCode: "EMAIL_VERIFICATION_REQUIRED")
        }) as Void

        gate.dismissRecovery()

        XCTAssertFalse(gate.isRecoveryPresented)
        XCTAssertEqual(mutationCount, 1)
    }

    func testRSSVoteRestoresAbsentVoteAndRequiresExplicitRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1/vote"] = (verificationError, 403)
        let viewModel = try RSSFeedListViewModel(client: makeClient(), contentType: .news)

        await viewModel.vote(rssFeedItemId: "item-1", choice: .like)

        XCTAssertNil(viewModel.myVotesByItemId["item-1"])
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        viewModel.emailVerificationGate.dismissRecovery()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1/vote"] = (Data("{}".utf8), 204)
        await viewModel.vote(rssFeedItemId: "item-1", choice: .like)
        XCTAssertEqual(viewModel.myVotesByItemId["item-1"], .like)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
    }

    func testStoryDiscussionVerificationFailurePreservesRetryState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/stories/story-1/discussions"] = (verificationError, 403)
        let viewModel = try RSSFeedListViewModel(client: makeClient(), contentType: .news)
        viewModel.storyIdsByItemId["item-1"] = "story-1"
        viewModel.storyMemberIdsByStoryId["story-1"] = ["item-1", "item-2"]

        let destination = await viewModel.startStoryDiscussion(rssFeedItemId: "item-1")

        XCTAssertNil(destination)
        XCTAssertTrue(viewModel.canStartStoryDiscussion(rssFeedItemId: "item-1"))
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        viewModel.emailVerificationGate.dismissRecovery()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)
    }

    func testComposeVerificationFailurePreservesDraftAndRequiresExplicitRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (verificationError, 403)
        let viewModel = try NativePostComposeViewModel(client: makeClient())
        viewModel.title = "Preserved title"
        viewModel.bodyText = "Preserved body"
        viewModel.turnstileToken = "spent-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.title, "Preserved title")
        XCTAssertEqual(viewModel.bodyText, "Preserved body")
        XCTAssertEqual(viewModel.drafts.first?.title, "Preserved title")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        viewModel.emailVerificationGate.dismissRecovery()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (successfulPostEnvelope, 201)
        viewModel.turnstileToken = "new-token"
        await viewModel.publish()
        XCTAssertEqual(viewModel.publishedPostId, "post-1")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
    }

    func testCommentVotePreservesVoteAndPresentsRecovery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-a/vote"] = (verificationError, 403)
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(),
            rootPostId: "root-1",
            currentUserId: "user-1"
        )
        viewModel.voteChoicesByPostId["comment-a"] = .like

        await viewModel.vote(postId: "comment-a", choice: .dislike)

        XCTAssertEqual(viewModel.voteChoicesByPostId["comment-a"], .like)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
    }

    func testReplyDoesNotApplyMutationAndPresentsRecovery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (verificationError, 403)
        let viewModel = try NativeCommentThreadViewModel(
            client: makeClient(),
            rootPostId: "root-1",
            currentUserId: "user-1"
        )

        await viewModel.reply(parentId: "comment-a", markdown: "Keep this reply")

        XCTAssertTrue(viewModel.descendantPosts.isEmpty)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
    }

    func testCommentSurfacePreservesReplyComposerAndMarkdownForVerificationRecovery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (verificationError, 403)
        let draft = NativeCommentThreadComposerState(
            kind: .reply(parentId: "comment-a"),
            markdown: "Keep **this** reply"
        )
        let surface = try NativeCommentThreadSurface(
            client: makeClient(),
            routeMatch: NativeRouteMatch(
                path: "/comment/root-1",
                template: "/comment/:id",
                params: ["id": "root-1"]
            ),
            currentUserId: "user-1",
            isSignedIn: true,
            composer: draft,
            showSignIn: {}
        )

        await surface.submitComposer(draft)

        XCTAssertEqual(surface.composer?.kind, .reply(parentId: "comment-a"))
        XCTAssertEqual(surface.composer?.markdown, "Keep **this** reply")
        XCTAssertTrue(surface.viewModel.emailVerificationGate.isRecoveryPresented)
    }

    func testTopicVoteRollsBackAndPresentsRecovery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/vote"] = (verificationError, 403)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient()
        )
        viewModel.myVotesByTopicId["topic-1"] = .like

        await viewModel.vote(topicId: "topic-1", choice: .dislike)

        XCTAssertEqual(viewModel.myVotesByTopicId["topic-1"], .like)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
    }

    func testFirstTopicVoteIsRemovedWhenVerificationIsRequired() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/topic-1/vote"] = (verificationError, 403)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient()
        )

        await viewModel.vote(topicId: "topic-1", choice: .like)

        XCTAssertNil(viewModel.myVotesByTopicId["topic-1"])
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
    }

    func testUserTrustVoteRollsBackAndPresentsRecovery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/vouch-vote"] = (verificationError, 403)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .userProfile),
            client: makeClient()
        )
        viewModel.userProfile.trustChoice = .like

        await viewModel.voteUserTrust(userId: "user-1", choice: .disavow)

        XCTAssertEqual(viewModel.userProfile.trustChoice, .like)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/users/user-1/vouch-vote")
    }

    func testSuccessfulUserDisavowReloadsRelationSidecar() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/vouch-vote"] = (Data("{}".utf8), 204)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-1"] = (
            Data(#"{"bookmarks":{"follow":false,"mute":true}}"#.utf8), 200
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .userProfile),
            client: makeClient()
        )
        viewModel.detailRelationEntityType = "user"
        viewModel.detailRelationEntityId = "user-1"
        viewModel.detailRelationBookmarks = ["follow": true, "mute": false]

        await viewModel.voteUserTrust(userId: "user-1", choice: .disavow)

        XCTAssertEqual(viewModel.userProfile.trustChoice, .disavow)
        XCTAssertFalse(viewModel.isDetailRelationActive("follow"))
        XCTAssertTrue(viewModel.isDetailRelationActive("mute"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/users/user-1/vouch-vote", "/api/v1/bookmarks/user/user-1"
        ])
    }

    private var verificationError: Data {
        Data(#"{"code":"EMAIL_VERIFICATION_REQUIRED"}"#.utf8)
    }

    private var successfulPostEnvelope: Data {
        Data("""
        {
          "post": {
            "id": "post-1",
            "slug": null,
            "post_type": "discussion",
            "title": "Preserved title",
            "markdown": "Preserved body",
            "html": null,
            "parent_id": null,
            "root_id": null,
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": "pending",
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
}
