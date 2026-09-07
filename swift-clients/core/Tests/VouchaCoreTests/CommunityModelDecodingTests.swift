import Foundation
@testable import VouchaModels
import XCTest

final class CommunityModelDecodingTests: XCTestCase {
    func testDecodesCommunityShowAndSearchResponses() throws {
        let decoder = makeVouchaDecoder()

        let show = try decoder.decode(
            CommunityResponse.self,
            from: Data(
                #"{"community":{"id":"community-1","name":"Test Community","slug":"test-community","markdown":"","visibility":"public","member_roster_visibility":"public","list_type":null,"member_invites_allowed_at":null,"post_approval_required_at":null,"allow_review_posts":false,"allow_data_point_posts":false,"trusted_at":null,"profile_image_id":null,"banner_image_id":null,"created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null,"deleted_by_id":null,"archived_at":null,"archived_by_id":null,"default_language":null,"lingua_rs_detected_language":null,"rules_markdown":null,"owner":null},"community_metrics":{"id":"community-1","member_count":3,"post_count":2,"list_item_count":5,"proxy_follow_count":1,"proxy_mute_count":0,"virtual_subscription_count":1},"membership":{"id":"membership-1","community_id":"community-1","user_id":"user-1","role":"member","approved_by_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","removed_at":null,"removed_by_id":null},"user":{"id":"user-1","username":"owner","roles":[],"profile_image_id":null,"markdown":null},"has_pending_application":true}"#
                    .utf8
            )
        )
        XCTAssertEqual(show.community.slug, "test-community")
        XCTAssertEqual(show.communityMetrics?.memberCount, 3)
        XCTAssertEqual(show.membership?.role, .member)
        XCTAssertEqual(show.user?.username, "owner")
        XCTAssertEqual(show.hasPendingApplication, true)

        let search = try decoder.decode(
            CommunitiesSearchResponse.self,
            from: Data(
                #"{"results":[{"id":"community-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"communities":{"community-1":{"id":"community-1","name":"Test Community","slug":"test-community","markdown":"","visibility":"public","member_roster_visibility":"public","list_type":"follow","member_invites_allowed_at":null,"post_approval_required_at":null,"allow_review_posts":false,"allow_data_point_posts":false,"trusted_at":null,"profile_image_id":null,"banner_image_id":null,"created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null,"deleted_by_id":null,"archived_at":null,"archived_by_id":null,"default_language":null,"lingua_rs_detected_language":null,"rules_markdown":null,"owner":null}},"community_metrics":{"community-1":{"id":"community-1","member_count":3,"post_count":2,"list_item_count":5,"proxy_follow_count":1,"proxy_mute_count":0,"virtual_subscription_count":1}},"users":{},"community_memberships":{},"pending_application_community_ids":["community-1"],"bookmarks":{"community-1":{"save":true,"follow":false}}}"#
                    .utf8
            )
        )
        XCTAssertEqual(search.results.first?.id, "community-1")
        XCTAssertEqual(search.communities["community-1"]?.listType, .follow)
        XCTAssertEqual(search.communityMetrics?["community-1"]?.virtualSubscriptionCount, 1)
        XCTAssertEqual(search.pendingApplicationCommunityIds, ["community-1"])
        XCTAssertEqual(search.bookmarks?["community-1"]?["save"], true)
        XCTAssertEqual(search.bookmarks?["community-1"]?["follow"], false)
    }

    func testDecodesCommunityApplicationAndListItemResponses() throws {
        let decoder = makeVouchaDecoder()

        let questions = try decoder.decode(
            CommunityApplicationQuestionsResponse.self,
            from: Data(
                #"{"questions":[{"id":"question-1","community_id":"community-1","question":"Why join?","field_type":"short_text","options":null,"order_index":0,"required":true,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null}]}"#
                    .utf8
            )
        )
        XCTAssertEqual(questions.questions.first?.fieldType, .shortText)

        let applications = try decoder.decode(
            CommunityApplicationsResponse.self,
            from: Data(
                #"{"results":[{"id":"application-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"community_applications":{"application-1":{"id":"application-1","community_id":"community-1","user_id":"user-1","answers":{"why":{"string":"Because"}},"message":"Hello","reviewed_at":null,"reviewed_by_id":null,"approved_at":null,"rejected_at":null,"rejection_reason":null,"created_at":"2026-01-01T00:00:00Z"}}}"#
                    .utf8
            )
        )
        XCTAssertEqual(applications.communityApplications["application-1"]?.message, "Hello")

        let pendingReports = try decoder.decode(
            CommunityPendingReportsResponse.self,
            from: ApiFixtureLoader.data("native.community.pending-reports.paginated")
        )
        let pendingReport = try XCTUnwrap(pendingReports.reports.first)
        XCTAssertEqual(pendingReport.id, "00000000-0000-7000-8000-000000000601")
        XCTAssertEqual(pendingReport.caseId, "00000000-0000-7000-8000-000000000602")
        XCTAssertEqual(pendingReport.status, .pending)
        XCTAssertEqual(pendingReport.reportCount, 3)
        XCTAssertNil(pendingReport.reviewedAt)
        XCTAssertNil(pendingReport.resolvedById)
        XCTAssertEqual(pendingReport.claim?.reportId, pendingReport.id)
        XCTAssertEqual(pendingReport.claim?.claimedById, "00000000-0000-7000-8000-000000000610")
        XCTAssertNil(pendingReport.claim?.releasedAt)
        XCTAssertEqual(pendingReport.escalatedById, "00000000-0000-7000-8000-000000000611")
        XCTAssertFalse(pendingReports.pageInfo.hasNextPage)
        XCTAssertEqual(pendingReports.pageInfo.hasPreviousPage, true)

        try assertFixtureContainsKeys(
            "web.communities.list-items.counts.default",
            keys: ["post", "rss_feed", "topic", "url", "url_hostname"]
        )

        XCTAssertEqual(CommunityListItemType.urlHostname.routeSegment, "domains")
        XCTAssertEqual(CommunityListItemType.rssFeed.routeSegment, "rss-feeds")

        let listItems = try decoder.decode(
            CommunityListItemsResponse.self,
            from: Data(
                #"{"results":[{"id":"item-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"community_list_items":{"item-1":{"id":"item-1","community_id":"community-1","item_type":"topic","entity_id":"topic-1","order_index":0,"added_by_id":null,"created_at":"2026-01-01T00:00:00Z"}}}"#
                    .utf8
            )
        )
        XCTAssertEqual(listItems.communityListItems["item-1"]?.itemType, .topic)
    }

    func testDecodesSharedCommunityFixtures() throws {
        let decoder = makeVouchaDecoder()

        let archive = try decoder.decode(
            CommunityResponse.self,
            from: ApiFixtureLoader.data("web.communities.archive.default")
        )
        XCTAssertEqual(archive.community.archivedById, "user-1")
        XCTAssertNotNil(archive.community.archivedAt)

        let members = try decoder.decode(
            CommunityMembersResponse.self,
            from: ApiFixtureLoader.data("web.communities.members.default")
        )
        XCTAssertEqual(members.results.map(\.id), ["community-member-1"])
        XCTAssertEqual(members.communityMembers["community-member-1"]?.role, .owner)

        try assertFixtureContainsKeys(
            "web.communities.list-items.counts.default",
            keys: ["post", "rss_feed", "topic", "url", "url_hostname"]
        )

        let listTopics = try decoder.decode(
            CommunityListItemsResponse.self,
            from: ApiFixtureLoader.data("web.communities.list-items.topics.default")
        )
        XCTAssertEqual(listTopics.communityListItems["list-item-topic"]?.itemType, .topic)

        let applications = try decoder.decode(
            CommunityApplicationsResponse.self,
            from: ApiFixtureLoader.data("web.communities.applications.default")
        )
        XCTAssertTrue(applications.results.isEmpty)
        XCTAssertTrue(applications.communityApplications.isEmpty)

        try assertFixtureContainsKeys(
            "web.communities.posts.default",
            keys: ["communities", "posts", "results"]
        )
        try assertFixtureContainsKeys(
            "web.communities.news.default",
            keys: ["results", "rss_feed_items"]
        )
        try assertFixtureContainsKeys(
            "web.communities.invites.default",
            keys: ["community_invites", "page_info", "results"]
        )
        try assertFixtureContainsKeys(
            "web.communities.bans.default",
            keys: ["community_bans", "page_info", "results"]
        )
        try assertFixtureContainsKeys(
            "web.communities.restrictions.default",
            keys: ["community_restrictions", "page_info", "raid_mode_suggestion", "results"]
        )
        try assertFixtureContainsKeys(
            "web.communities.moderation-queue.default",
            keys: ["entries", "page_info", "viewer_tier"]
        )
        try assertFixtureContainsKeys(
            "web.communities.moderation-analytics.default",
            keys: ["appeals", "automod_performance", "moderator_workload", "queue_volume"]
        )
        try assertFixtureContainsKeys(
            "web.communities.ai-agents.default",
            keys: ["community_ai_agents"]
        )
        try assertFixtureContainsKeys(
            "web.communities.agent-prompts.default",
            keys: ["community_agent_prompts", "slot_info"]
        )
        try assertFixtureContainsKeys(
            "web.communities.automod-simulate.default",
            keys: ["results", "simulation"]
        )
    }

    func testDecodesModerationAndModmailResponsesWithPermissiveFields() throws {
        let decoder = makeVouchaDecoder()

        let aiAgent = try decoder.decode(
            CommunityAiAgentResponse.self,
            from: Data(
                #"{"community_ai_agent":{"agent_id":"agent-1","always_on":false,"enabled":true,"enabled_at":"2026-01-01T00:00:00Z","enabled_by_id":"user-1","label_topic_slugs":[],"on_flag_action":"unpublish","slug":"self-promotion","system_user_id":"system-user-1","system_username":"community_agent"}}"#
                    .utf8
            )
        )
        XCTAssertEqual(aiAgent.communityAiAgent.onFlagAction, .unpublish)
        XCTAssertNil(aiAgent.communityAiAgent.entitlement)

        let thread = try decoder.decode(
            CommunityModmailThreadResponse.self,
            from: Data(
                #"{"thread":{"id":"thread-1","channel_type":"modmail","title":"Modmail","community_id":"community-1","subject_user_id":null,"assigned_mod_id":null,"assigned_at":null,"resolved_at":null,"resolved_by_id":null,"created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z"}}"#
                    .utf8
            )
        )
        XCTAssertNil(thread.thread.subjectUserId)

        let messages = try decoder.decode(
            CommunityModmailMessagesResponse.self,
            from: Data(
                #"{"results":[{"id":"message-1","conversation_id":"thread-1","body_text":"Hello mod team","created_by_id":"user-1","sender_username":"moderator","created_at":"2026-01-01T00:00:00Z","deleted_at":null}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#
                    .utf8
            )
        )
        XCTAssertEqual(messages.results.first?.bodyText, "Hello mod team")

        let bans = try decoder.decode(
            CommunityBansResponse.self,
            from: Data(
                #"{"results":[{"id":"ban-1"}],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null},"community_bans":{"ban-1":{"id":"ban-1","community_id":"community-1","user_id":"user-1","banned_by_id":null,"reason":null,"expires_at":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","lifted_at":null,"lifted_by_id":null}},"users":{}}"#
                    .utf8
            )
        )
        XCTAssertNil(bans.communityBans["ban-1"]?.caseId)

        let stats = try decoder.decode(
            CommunityModeratorStatsResponse.self,
            from: Data(
                #"{"window":30,"stats":[{"actor_id":"user-1","total":2}],"users":{"user-1":{"id":"user-1","username":"moderator","roles":[],"profile_image_id":null,"markdown":null}}}"#
                    .utf8
            )
        )
        XCTAssertNil(stats.stats.first?.counts)

        let simulation = try decoder.decode(
            CommunityAutomodSimulation.self,
            from: Data(
                #"{"simulation":{"prompt_id":"prompt-1","time_window_hours":24,"sample_count":1,"would_flag_count":1,"would_unpublish_count":1,"false_positive_estimate":null},"results":[{"post_id":"post-1","title":"Test post","declared_language":null,"lingua_rs_detected_language":null,"post_type":"discussion","approved_at":"2026-01-01T00:00:00Z","content_excerpt":"excerpt","flagged":true,"reason":"Spam","would_unpublish":true}]}"#
                    .utf8
            )
        )
        XCTAssertEqual(simulation.results.first?.postType, "discussion")
        XCTAssertNotNil(simulation.results.first?.approvedAt)
    }

    private func assertFixtureContainsKeys(
        _ fixtureId: String,
        keys: [String],
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let object = try JSONSerialization.jsonObject(with: ApiFixtureLoader.data(fixtureId))
        let dictionary = try XCTUnwrap(object as? [String: Any], file: file, line: line)
        for key in keys {
            XCTAssertNotNil(dictionary[key], "Missing key \(key) in fixture \(fixtureId)", file: file, line: line)
        }
    }
}
