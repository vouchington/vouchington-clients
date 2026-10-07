import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityDetailActionTests: NativeRouteSurfaceViewModelTestCase {
    func testMemberListApplicationInviteAndPinnedActionsCallCommunityEndpoints() async throws {
        registerCommunityAdminFixtures()
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "builders", initialTab: .members)

        await viewModel.updateMemberRole(userId: "user-2", role: .moderator)
        await viewModel.removeMember(userId: "user-2")
        await viewModel.transferOwnership(userId: "user-3")

        viewModel.selectedTab = .listTopics
        await viewModel.addListItem(itemType: .topic, entityId: "topic-2")
        await viewModel.addListItem(itemType: .rssFeed, entityId: "feed-2")
        await viewModel.addListItem(itemType: .post, entityId: "post-2")
        await viewModel.addListItem(itemType: .urlHostname, entityId: "domain-2")
        await viewModel.addListItem(itemType: .url, entityId: "url-2")
        await viewModel.removeListItem(itemType: .topic, itemId: "item-1")

        viewModel.selectedTab = .applications
        await viewModel.approveApplication(applicationId: "application-1")
        await viewModel.rejectApplication(applicationId: "application-1", reason: "Needs more detail")

        viewModel.selectedTab = .invites
        await viewModel.revokeInvite(inviteId: "invite-1")

        viewModel.selectedTab = .pinnedPosts
        await viewModel.updatePinnedPosts(postIds: ["post-1", "post-2"])
        await viewModel.updatePinnedPosts(postIds: [])

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/members/user-2" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/ownership-transfers" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/list-items/topics" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/list-items/rss-feeds" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/list-items/posts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/list-items/domains" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/list-items/urls" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/applications/application-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/invites/invite-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/pinned-posts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""role":"moderator""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""topic_id":"topic-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""rss_feed_id":"feed-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""post_id":"post-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""url_hostname_id":"domain-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""url_id":"url-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""status":"approved""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""status":"rejected""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""post_ids":["post-1","post-2"]"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""post_ids":[]"#) == true })
    }

    func testModerationBanRestrictionAndModmailActionsCallCommunityEndpoints() async throws {
        registerCommunityAdminFixtures()
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation,
            modmailThreadId: "thread-1"
        )

        await viewModel.approvePendingPost(postId: "post-1")
        await viewModel.rejectPendingPost(postId: "post-1", reason: "Needs sources")
        await viewModel.unpublishPendingPost(postId: "post-1")
        await viewModel.claimPendingPost(postId: "post-1")
        await viewModel.releasePendingPost(postId: "post-1")
        await viewModel.escalatePendingPost(postId: "post-1")
        CannedFeedURLProtocol.handlers[
            "/api/v1/communities/builders/posts/post-1/moderation-results"
        ] = (
            Data("""
            {
              "community_agent_moderations": [],
              "platform_moderation": { "status": "in_review" }
            }
            """.utf8),
            200
        )
        await viewModel.loadModerationResults(postId: "post-1")
        XCTAssertEqual(viewModel.moderationResults.map(\.title), ["AI agents", "Moderation summary"])
        XCTAssertEqual(viewModel.moderationResults.map(\.detail), ["0 results", "In review"])

        await viewModel.claimModerationReport(reportId: "report-1")
        await viewModel.releaseModerationReport(reportId: "report-1")
        await viewModel.escalateModerationReport(reportId: "report-1")
        await viewModel.issueCommunityWarning(
            userId: "user-2",
            reason: "Be civil",
            publicMessage: "Please review the rules.",
            reportId: "report-1",
            resolveReport: true
        )

        viewModel.selectedTab = .bans
        await viewModel.banMember(userId: "user-2", reason: "spam", expiresAt: nil)
        await viewModel.liftBan(userId: "user-2")

        viewModel.selectedTab = .restrictions
        await viewModel.activateRestrictions(
            restrictionTypes: [.requirePostApproval, .noNewMemberPosts],
            reason: "cooldown"
        )
        await viewModel.liftRestriction(restrictionId: "restriction-1")

        viewModel.selectedTab = .modmail
        await viewModel.openModmailThread(subjectUserId: "user-2")
        await viewModel.sendModmailMessage(conversationId: "thread-1", text: "Hello")
        await viewModel.resolveModmailThread(conversationId: "thread-1")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/posts/post-1/escalation" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/communities/builders/posts/post-1/moderation-results"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/reports/report-1/escalation" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/warnings" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/communities/builders/bans" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/bans/user-2" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/restrictions" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/restrictions/restriction-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/modmail/thread-1/messages" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/modmail" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/communities/builders/modmail/thread-1" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""reason":"Needs sources""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""status":"unpublished""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""reportId":"report-1""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""resolveReport":true"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""subject_user_id":"user-2""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies
            .contains { $0?.contains(#""restriction_types":["require_post_approval","no_new_member_posts"]"#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""text":"Hello""#) == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains(#""resolved":true"#) == true })
    }

    func registerCommunityAdminFixtures() {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Builders",
                    "slug": "builders",
                    "markdown": "Native community",
                    "visibility": "public",
                    "member_roster_visibility": "public",
                    "list_type": null,
                    "member_invites_allowed_at": "2026-07-01T00:00:00Z",
                    "post_approval_required_at": "2026-07-01T00:00:00Z",
                    "automod_action": "record_only",
                    "should_allow_review_posts": true,
                    "should_allow_data_point_posts": true,
                    "trusted_at": null,
                    "profile_image_id": null,
                    "banner_image_id": null,
                    "created_by_id": "user-1",
                    "created_at": "2026-07-01T00:00:00Z",
                    "updated_at": "2026-07-01T00:00:00Z",
                    "deleted_at": null,
                    "deleted_by_id": null,
                    "archived_at": null,
                    "archived_by_id": null,
                    "default_language": null,
                    "lingua_rs_detected_language": null,
                    "rules_markdown": null
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 7,
                    "post_count": 4,
                    "list_item_count": 5,
                    "proxy_follow_count": 0,
                    "proxy_mute_count": 0,
                    "virtual_subscription_count": 0
                  },
                  "membership": {
                    "id": "membership-1",
                    "community_id": "community-1",
                    "user_id": "user-1",
                    "role": "owner",
                    "approved_by_id": null,
                    "created_at": "2026-07-01T00:00:00Z",
                    "updated_at": "2026-07-01T00:00:00Z",
                    "removed_at": null,
                    "removed_by_id": null
                  },
                  "has_pending_application": false
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            Data(#"{"topic":1,"rss_feed":1,"post":1,"url_hostname":1,"url":1}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (
            Data(
                """
                {
                  "results": [{ "id": "membership-2" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_members": {
                    "membership-2": {
                      "id": "membership-2",
                      "community_id": "community-1",
                      "user_id": "user-2",
                      "role": "member",
                      "approved_by_id": null,
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": "2026-07-01T00:00:00Z",
                      "removed_at": null,
                      "removed_by_id": null
                    }
                  },
                  "users": {
                    "user-2": { "id": "user-2", "username": "member-2" }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/topics"] = (
            Data(
                """
                {
                  "results": [{ "id": "item-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_list_items": {
                    "item-1": {
                      "id": "item-1",
                      "community_id": "community-1",
                      "item_type": "topic",
                      "entity_id": "topic-1",
                      "order_index": 0,
                      "added_by_id": "user-1",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = (
            Data(
                """
                {
                  "results": [{ "id": "application-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_applications": {
                    "application-1": {
                      "id": "application-1",
                      "community_id": "community-1",
                      "user_id": "user-2",
                      "answers": {},
                      "message": "Let me in",
                      "reviewed_at": null,
                      "reviewed_by_id": null,
                      "approved_at": null,
                      "rejected_at": null,
                      "rejection_reason": null,
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/application-questions"] = (
            Data(
                #"{"questions":[{"id":"question-1","community_id":"community-1","question":"Why?","field_type":"short_text","options":null,"order_index":0,"is_required":true,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z","deleted_at":null}]}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/invites"] = (
            Data(
                """
                {
                  "results": [{ "id": "invite-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_invites": {
                    "invite-1": {
                      "id": "invite-1",
                      "community_id": "community-1",
                      "code": "code-1",
                      "invited_user_id": null,
                      "invited_email": "person@example.com",
                      "invited_by_id": "user-1",
                      "accepted_at": null,
                      "accepted_by_user_id": null,
                      "declined_at": null,
                      "revoked_at": null,
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/pinned-posts"] = (
            Data(
                """
                {
                  "pinned_posts": [
                    {
                      "community_id": "community-1",
                      "post_id": "post-1",
                      "order_index": 0,
                      "pinned_by_id": "user-1",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  ]
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts/pending"] = (
            Data(
                """
                {
                  "results": [{ "id": "post-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "posts": {
                    "post-1": {
                      "id": "post-1",
                      "post_type": "discussion",
                      "title": "Pending post",
                      "slug": "pending-post",
                      "markdown": "Body",
                      "created_by_id": "user-2",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  },
                  "posts_metrics": {},
                  "communities": {}
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail"] = (
            Data(#"{"conversation":{"id":"thread-1"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/automod/recent-actions"] = (
            Data(
                """
                {
                  "automod_actions": [
                    {
                      "source_key": "source-1",
                      "source_type": "community_prompt",
                      "post_id": "post-1",
                      "community_id": "community-1",
                      "agent_moderation_id": null,
                      "moderator_slug": "automod",
                      "title": "Flagged post",
                      "declared_language": null,
                      "lingua_rs_detected_language": null,
                      "markdown_preview": "Spam content",
                      "post_type": "discussion",
                      "post_href": "/p/post-1",
                      "created_at": "2026-07-01T00:00:00Z",
                      "action_at": "2026-07-01T00:01:00Z",
                      "confidence_score": 0.94,
                      "is_flagged": true,
                      "reason": "Spam",
                      "categories": ["spam"],
                      "model_output": {},
                      "current_state": "unpublished",
                      "feedback_label": null
                    }
                  ],
                  "stats": {
                    "total_count": 1,
                    "false_positive_count": 0,
                    "false_positive_rate": 0
                  },
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail/thread-1/messages"] = (
            Data(
                """
                {
                  "results": [
                    {
                      "id": "message-1",
                      "conversation_id": "thread-1",
                      "body_text": "Please review",
                      "created_by_id": "user-2",
                      "sender_username": "member",
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  ],
                  "page_info": { "has_next_page": false, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-vacation"] = (
            Data(#"{"vacation":null,"should_suppress_community_digests_while_on_vacation":false}"#.utf8),
            200
        )
    }
}
