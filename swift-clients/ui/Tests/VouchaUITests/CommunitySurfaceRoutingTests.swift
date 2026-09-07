// swiftlint:disable file_length
import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunitySurfaceRoutingTests: NativeRouteSurfaceViewModelTestCase {
    func testDetailLoadRowsHydratesEachTab() async throws {
        registerDetailRowFixtures()
        let viewModel = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
        await viewModel.load()
        XCTAssertEqual(viewModel.state, .loaded)

        viewModel.selectedTab = .posts
        let postRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(postRows, [
            verbatimRow(icon: "doc.text", title: "Post title", detail: "Discussion")
        ])

        viewModel.selectedTab = .news
        let newsRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(newsRows, [
            verbatimRow(icon: "newspaper", title: "News title", detail: "Community feed")
        ])

        viewModel.selectedTab = .members
        let memberRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(memberRows, [
            verbatimRow(icon: "person", title: "alice", detail: "Member")
        ])

        viewModel.selectedTab = .lists
        let listRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(listRows, [
            verbatimRow(icon: "tag", title: "Topics", detail: "1 item"),
            verbatimRow(icon: "dot.radiowaves.left.and.right", title: "Sources", detail: "2 items"),
            verbatimRow(icon: "doc.text", title: "Posts", detail: "3 items"),
            verbatimRow(icon: "link", title: "Domains and URLs", detail: "9 items")
        ])

        viewModel.selectedTab = .pinnedPosts
        let pinnedRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(pinnedRows, [
            verbatimRow(icon: "pin", title: "Pinned post post-1", detail: "Order 1")
        ])

        viewModel.selectedTab = .applications
        let applicationRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(applicationRows, [
            verbatimRow(
                icon: "doc.badge.clock",
                title: "Application application-1",
                detail: "Awaiting review"
            )
        ])

        viewModel.selectedTab = .invites
        let inviteRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(inviteRows, [
            verbatimRow(icon: "envelope", title: "person@example.com", detail: "Open")
        ])

        viewModel.selectedTab = .moderation
        let moderationRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(moderationRows, [
            verbatimRow(icon: "checklist", title: "Pending posts", detail: "1 queued"),
            verbatimRow(icon: "exclamationmark.triangle", title: "Pending reports", detail: "1 queued")
        ])

        viewModel.selectedTab = .listTopics
        let listTopicRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(listTopicRows, [
            verbatimRow(icon: "tag", title: "topic-1", detail: "Order 1")
        ])

        viewModel.selectedTab = .modlog
        let modlogRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(modlogRows, [
            verbatimRow(icon: "list.bullet.rectangle", title: "ban", detail: "moderator · alice · Spam")
        ])

        viewModel.selectedTab = .modmail
        let modmailRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(modmailRows, [
            verbatimRow(icon: "bubble.left.and.bubble.right", title: "Thread thread-1", detail: "Open")
        ])

        viewModel.selectedTab = .moderatorVacation
        let vacationRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(vacationRows, [
            verbatimRow(icon: "moon.zzz", title: "Moderator vacation", detail: "Active")
        ])
        XCTAssertTrue(viewModel.suppressCommunityDigestsWhileOnVacation)
        let modmailThreadViewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail,
            modmailThreadId: "thread-1"
        )
        let modmailThreadRows = try await modmailThreadViewModel.loadRows(client: makeClient())
        XCTAssertEqual(modmailThreadRows, [
            verbatimRow(
                icon: "bubble.left.and.text.bubble.right",
                title: "@moderator",
                detail: "Please review"
            )
        ])

        viewModel.selectedTab = .bans
        let banRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(banRows, [
            verbatimRow(icon: "nosign", title: "alice", detail: "Spam · No expiry")
        ])

        viewModel.selectedTab = .restrictions
        let restrictionRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(restrictionRows, [
            verbatimRow(icon: "lock.shield", title: "Approved members only", detail: "Active · Raid")
        ])

        viewModel.selectedTab = .moderationAnalytics
        let analyticsRows = try await viewModel.loadRows(client: makeClient())
        XCTAssertEqual(analyticsRows.first, verbatimRow(
            icon: "chart.bar",
            title: "Queue volume",
            detail: "0 reports · 0 pending"
        ))
    }

    private func registerDetailRowFixtures() {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders"] = (
            Data(
                """
                {
                  "community": {
                    "id": "community-1",
                    "name": "Native Builders",
                    "slug": "builders",
                    "markdown": "SwiftUI community",
                    "visibility": "public",
                    "member_roster_visibility": "public",
                    "list_type": "follow",
                    "member_invites_allowed_at": "2026-07-01T00:00:00Z",
                    "post_approval_required_at": null,
                    "allow_review_posts": true,
                    "allow_data_point_posts": true,
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
                    "default_language": "en",
                    "lingua_rs_detected_language": null,
                    "rules_markdown": "Be kind."
                  },
                  "community_metrics": {
                    "id": "community-1",
                    "member_count": 1,
                    "post_count": 1,
                    "list_item_count": 1,
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
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts"] = (
            Data(
                """
                {
                  "results": [{ "__entity_type": "post", "id": "post-1" }],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
                  "posts": {
                    "post-1": {
                      "id": "post-1",
                      "post_type": "discussion",
                      "title": "Post title",
                      "slug": "post-title",
                      "markdown": "Body",
                      "html": null,
                      "parent_id": null,
                      "root_id": null,
                      "created_by_id": "user-1",
                      "created_at": "2026-07-01T00:00:00.000Z",
                      "broadcast": null,
                      "privacy": "public",
                      "is_anonymous": false,
                      "community_id": "community-1",
                      "clearance_status": "approved",
                      "metrics": null,
                      "election": null,
                      "created_by": null,
                      "updated_at": null,
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
                      "can_edit_content": null,
                      "can_delete": null,
                      "can_lock": null
                    }
                  },
                  "posts_metrics": {},
                  "communities": {}
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/news"] = (
            Data(
                """
                {
                  "results": [{ "id": "result-1", "entity_id": "item-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "rss_feed_items": {
                    "item-1": {
                      "id": "item-1",
                      "rss_feed_id": "feed-1",
                      "url": "https://example.com/news",
                      "title": "News title",
                      "data": null,
                      "rss_feed": {
                        "id": "feed-1",
                        "title": "Community feed",
                        "feed_type": "article",
                        "rss_feed_url": { "url": "https://example.com/feed.xml", "canonical_url_id": null },
                        "hostname": null,
                        "topic": null,
                        "publisher_type": null,
                        "podcast_show": null
                      }
                    }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/members"] = (
            Data(
                """
                {
                  "results": [{ "id": "member-1" }],
                  "page_info": { "has_next_page": false, "end_cursor": null },
                  "community_members": {
                    "member-1": {
                      "id": "member-1",
                      "community_id": "community-1",
                      "user_id": "user-1",
                      "role": "member",
                      "approved_by_id": "user-2",
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": "2026-07-01T00:00:00Z",
                      "removed_at": null,
                      "removed_by_id": null
                    }
                  },
                  "users": { "user-1": { "id": "user-1", "username": "alice", "roles": ["user"] } }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/counts"] = (
            Data(#"{"topic":1,"rss_feed":2,"post":3,"url_hostname":4,"url":5}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/list-items/topics"] = (
            Data(
                #"{"results":[{"id":"item-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"community_list_items":{"item-1":{"id":"item-1","community_id":"community-1","item_type":"topic","entity_id":"topic-1","order_index":0,"added_by_id":null,"created_at":"2026-07-01T00:00:00Z"}}}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/pinned-posts"] = (
            Data(
                #"{"pinned_posts":[{"community_id":"community-1","post_id":"post-1","order_index":0,"pinned_by_id":"user-1","created_at":"2026-07-01T00:00:00Z"}]}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/applications"] = (
            Data(
                #"{"results":[{"id":"application-1"}],"page_info":{"has_next_page":false,"end_cursor":null},"community_applications":{}}"#
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
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts/pending"] = (
            Data(
                #"{"results":[{"id":"post-1"}],"page_info":{"has_next_page":false,"end_cursor":null},"posts":{},"posts_metrics":{},"communities":{}}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/reports/pending"] = (
            Data(
                """
                {"reports":[{"id":"report-1","case_id":"case-report-1","entity_type":"post","entity_id":"post-report-1","target_label":"report-1","target_path":null,"target_content":null,"reason":"spam","status":"pending","report_count":1,"created_at":"2026-01-01T00:00:00Z","reviewed_at":null,"target_user_id":null,"reporter_user_id":null,"reporter_username":null,"note":null,"resolved_by_id":null,"admin_action_path":null,"target_available":true,"judgement":null,"community_ban_evasion":null,"claim":null,"escalated_at":null,"escalated_by_id":null}],"page_info":{"has_next_page":false,"end_cursor":null,"start_cursor":null}}
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modlog"] = (
            Data(
                """
                {
                  "results": [{ "id": "modlog-1" }],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
                  "moderator_actions": {
                    "modlog-1": {
                      "id": "modlog-1",
                      "community_id": "community-1",
                      "actor_id": "user-1",
                      "action_type": "ban",
                      "post_id": null,
                      "target_user_id": "user-2",
                      "report_id": null,
                      "review_dispute_id": null,
                      "community_application_id": null,
                      "reason": "Spam",
                      "metadata": {},
                      "created_at": "2026-07-01T00:00:00Z"
                    }
                  },
                  "users": {
                    "user-1": { "id": "user-1", "username": "moderator", "roles": ["user"] },
                    "user-2": { "id": "user-2", "username": "alice", "roles": ["user"] }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/modmail"] = (
            Data(
                """
                {
                  "results": [
                    {
                      "id": "thread-1",
                      "channel_type": "modmail",
                      "title": "Modmail",
                      "community_id": "community-1",
                      "subject_user_id": null,
                      "assigned_mod_id": null,
                      "assigned_at": null,
                      "resolved_at": null,
                      "resolved_by_id": null,
                      "created_by_id": "mod-1",
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": "2026-07-01T00:00:00Z"
                    }
                  ],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null }
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
                      "created_by_id": "mod-1",
                      "sender_username": "moderator",
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": null,
                      "deleted_at": null
                    }
                  ],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/bans"] = (
            Data(
                """
                {
                  "results": [{ "id": "ban-1" }],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
                  "community_bans": {
                    "ban-1": {
                      "id": "ban-1",
                      "community_id": "community-1",
                      "user_id": "user-2",
                      "banned_by_id": null,
                      "reason": "Spam",
                      "expires_at": null,
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": "2026-07-01T00:00:00Z",
                      "lifted_at": null,
                      "lifted_by_id": null
                    }
                  },
                  "users": {
                    "user-2": { "id": "user-2", "username": "alice", "roles": ["user"] }
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/restrictions"] = (
            Data(
                """
                {
                  "results": [{ "id": "restriction-1" }],
                  "page_info": { "has_next_page": false, "start_cursor": null, "end_cursor": null },
                  "community_restrictions": {
                    "restriction-1": {
                      "id": "restriction-1",
                      "community_id": "community-1",
                      "restriction_type": "approved_members_only",
                      "activated_by_id": null,
                      "activated_at": "2026-07-01T00:00:00Z",
                      "expires_at": null,
                      "created_at": "2026-07-01T00:00:00Z",
                      "updated_at": "2026-07-01T00:00:00Z",
                      "lifted_at": null,
                      "lifted_by_id": null,
                      "reason": "Raid"
                    }
                  },
                  "raid_mode_suggestion": {
                    "velocity_spike": true,
                    "flag_count": 2,
                    "latest_flagged_at": "2026-07-01T00:00:00Z"
                  }
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-analytics"] = (
            ApiFixtureLoader.data("web.communities.moderation-analytics.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-transparency"] = (
            ApiFixtureLoader.data("native.community.moderation-transparency.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderator-vacation"] = (
            Data(
                #"{"vacation":{"community_id":"community-1","user_id":"user-1","starts_at":"2026-07-01T00:00:00Z","ends_at":null,"created_at":"2026-07-01T00:00:00Z","updated_at":"2026-07-01T00:00:00Z"},"suppress_community_digests_while_on_vacation":true}"#
                    .utf8
            ),
            200
        )
    }
}
