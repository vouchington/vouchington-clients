import VouchaAPI
import VouchaModels

let nativeMembershipFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.memberships.plans.default") {
        try assertFixtureCoversDTO($0, as: MembershipPlansResponse.self)
    },
    RegisteredFixture(id: "native.memberships.grant.default") {
        try assertFixtureCoversDTO($0, as: MembershipGrantResponse.self)
    },
    RegisteredFixture(id: "native.identity-verification-attempts.grant.default") {
        try assertFixtureCoversDTO($0, as: IdentityVerificationAttemptGrantResponse.self)
    }
]

let nativeModerationCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.moderation.reports.default") {
        try assertFixtureCoversDTO(
            $0,
            as: StaffFlatModerationReportsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.resolved_by_id",
                "results.reviewed_at"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.reports.member.default") {
        try assertFixtureCoversDTO(
            $0,
            as: MemberFlatModerationReportsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor", "results.reviewed_at"]
        )
    },
    RegisteredFixture(id: "native.moderation.reports.clustered.default") {
        try assertFixtureCoversDTO(
            $0,
            as: StaffClusteredModerationReportsResponse.self,
            ignoring: [
                "results.reports.resolved_by_id",
                "results.reports.reviewed_at",
                "results.reports.judgement",
                "results.target_user_id",
                "duplicate_clusters.clusters.reports.resolved_by_id",
                "duplicate_clusters.clusters.reports.reviewed_at",
                "duplicate_clusters.clusters.reports.judgement",
                "duplicate_clusters.clusters.target_user_id",
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.reports.clustered.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: StaffClusteredModerationReportsResponse.self,
            ignoring: [
                "results.reports.resolved_by_id",
                "results.reports.reviewed_at",
                "results.reports.judgement",
                "results.target_user_id",
                "duplicate_clusters.clusters.reports.resolved_by_id",
                "duplicate_clusters.clusters.reports.reviewed_at",
                "duplicate_clusters.clusters.reports.judgement",
                "duplicate_clusters.clusters.target_user_id",
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.report-judgement.default") {
        try assertFixtureCoversDTO($0, as: ModerationReportJudgementResponse.self)
    },
    RegisteredFixture(id: "native.moderation.report-resolution.reviewed") {
        try assertFixtureCoversDTO($0, as: ModerationReportResolutionResponse.self)
    },
    RegisteredFixture(id: "native.moderation.admin-warning.report") {
        try assertFixtureCoversDTO(
            $0,
            as: AdminUserWarningResponse.self,
            ignoring: [
                "warning.community_id",
                "warning.revoked_at",
                "warning.revoked_by_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.ban-evasion.confirm") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.moderation.ban-evasion.dismiss") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.moderation.appeals.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationAppealListResponse.self,
            ignoring: [
                "appeals.approved_at",
                "appeals.approved_by_id",
                "appeals.community_ban_id",
                "appeals.community_id",
                "appeals.drafted_at",
                "appeals.edited_at",
                "appeals.edited_by_id",
                "appeals.internal_notes",
                "appeals.latest_lifecycle_change_id",
                "appeals.public_response",
                "appeals.resolution_action",
                "appeals.resolved_at",
                "appeals.resolved_by_id",
                "appeals.sent_at",
                "appeals.user_warning_id",
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.disputes.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModerationDisputeListResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
        try assertFixtureCoversDTO(
            $0,
            as: ReviewDisputeListResponse.self,
            ignoring: [
                "disputes.ai_drafted_at",
                "disputes.ai_internal_response",
                "disputes.ai_public_response",
                "disputes.approved_at",
                "disputes.approved_by_id",
                "disputes.drafted_at",
                "disputes.edited_at",
                "disputes.edited_by_id",
                "disputes.internal_notes",
                "disputes.latest_lifecycle_change_id",
                "disputes.model",
                "disputes.public_response",
                "disputes.resolution_action",
                "disputes.resolved_at",
                "disputes.resolved_by_id",
                "disputes.sent_at",
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.review-queue.default") {
        try assertFixtureCoversDTO(
            $0,
            as: AdminReviewQueueResponse.self,
            ignoring: []
        )
    },
    RegisteredFixture(id: "native.moderation.review-queue.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: AdminReviewQueueResponse.self,
            ignoring: ["results.root_id", "results.root_post_type", "results.root_slug"]
        )
    },
    RegisteredFixture(id: "native.moderation.clearance.approved") {
        try assertFixtureCoversDTO($0, as: ClearanceUpdateResponse.self)
    },
    RegisteredFixture(id: "native.moderation.clearance.rejected") {
        try assertFixtureCoversDTO($0, as: ClearanceUpdateResponse.self)
    },
    RegisteredFixture(id: "native.moderation.clearance.in-review") {
        try assertFixtureCoversDTO($0, as: ClearanceUpdateResponse.self)
    },
    RegisteredFixture(id: "native.moderation.modlog.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModlogResponse.self,
            ignoring: [
                "moderator_actions.action-1.community_id",
                "moderator_actions.action-1.reported_user_id",
                "moderator_actions.action-1.review_dispute_id",
                "moderator_actions.reported_user_id",
                "moderator_actions.action-1.target_user_id",
                "moderator_actions.target_user_id",
                "page_info.end_cursor",
                "users.admin-1.profile_image_id",
                "users.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.analytics.default") {
        try assertFixtureCoversDTO($0, as: ModerationAnalytics.self)
    }
] + nativeModerationIntegrityCoverage

let nativeListMessageCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.comments.post-detail.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.comments.ancestors.permalink") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.lists.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ListsSearchResponse.self,
            ignoring: [
                "lists.list-1.__entity_type",
                "lists.list-1.removed_at",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.list-items.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ListItemsResponse.self,
            ignoring: [
                "list_items.list-item-1.__entity_type",
                "list_items.list-item-2.__entity_type",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.lists-containing.default") {
        try assertFixtureCoversDTO($0, as: ListsContainingResponse.self)
    },
    RegisteredFixture(id: "native.list-import.default") {
        try assertFixtureCoversDTO($0, as: ImportCommunityListResponse.self)
    },
    RegisteredFixture(id: "native.landing-pages.default") {
        try assertFixtureCoversDTO($0, as: MyLandingPageListResponse.self)
    },
    RegisteredFixture(id: "native.landing-page-detail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: LandingPageDetailResponse.self,
            ignoring: ["landing_page.items.profile_link.handle", "landing_page.items.profile_link.image_id"]
        )
    },
    RegisteredFixture(id: "native.landing-page-analytics.default") {
        try assertFixtureCoversDTO($0, as: LandingPageAnalyticsResponse.self)
    },
    RegisteredFixture(id: "native.admin-user-landing-pages.default") {
        try assertFixtureCoversDTO($0, as: LandingPageListResponse.self, ignoring: ["page_info.end_cursor"])
    },
    RegisteredFixture(id: "native.admin-landing-page-analytics.default") {
        try assertFixtureCoversDTO(
            $0,
            as: AdminLandingPageAnalyticsResponse.self,
            ignoring: ["landing_page.items.profile_link.handle", "landing_page.items.profile_link.image_id"]
        )
    },
    RegisteredFixture(id: "native.landing-page-candidates.default") {
        try assertFixtureCoversDTO(
            $0,
            as: LandingPageCandidatesResponse.self,
            ignoring: ["candidates.profile_links.handle", "candidates.profile_links.image_id"]
        )
    },
    RegisteredFixture(id: "native.landing-page-items-mutation.default") {
        try assertFixtureCoversDTO(
            $0,
            as: LandingPageDetailResponse.self,
            ignoring: ["landing_page.items.profile_link.handle", "landing_page.items.profile_link.image_id"]
        )
    },
    RegisteredFixture(id: "native.landing-page-mutation.default") {
        try assertFixtureCoversDTO($0, as: LandingPageSummaryResponse.self, ignoring: ["landing_page.subtitle"])
    },
    RegisteredFixture(id: "native.messages.conversations.default") {
        try assertFixtureCoversDTO($0, as: Page<DirectConversation>.self, ignoring: ["page_info.end_cursor"])
    },
    RegisteredFixture(id: "native.messages.conversations.page-2") {
        try assertFixtureCoversDTO($0, as: Page<DirectConversation>.self, ignoring: ["page_info.end_cursor"])
    },
    RegisteredFixture(id: "native.messages.conversation.default") {
        try assertFixtureCoversDTO($0, as: DirectConversationResponse.self)
    },
    RegisteredFixture(id: "native.messages.create.default") {
        try assertFixtureCoversDTO($0, as: DirectConversationResponse.self)
    },
    RegisteredFixture(id: "native.messages.thread.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<DirectMessage>.self,
            ignoring: ["page_info.end_cursor", "results.deleted_at"]
        )
    },
    RegisteredFixture(id: "native.messages.thread.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<DirectMessage>.self,
            ignoring: ["page_info.end_cursor", "results.deleted_at"]
        )
    },
    RegisteredFixture(id: "native.messages.send.default") {
        try assertFixtureCoversDTO($0, as: DirectMessageResponse.self, ignoring: ["message.deleted_at"])
    },
    RegisteredFixture(id: "native.messages.participants.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<ConversationParticipant>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor", "results.removed_at"]
        )
    },
    RegisteredFixture(id: "native.messages.participant-add.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ConversationParticipantResponse.self,
            ignoring: ["participant.profile_image_id", "participant.removed_at"]
        )
    },
    RegisteredFixture(id: "native.messages.policy.default") {
        try assertFixtureCoversDTO($0, as: ConversationParticipantPolicyResponse.self)
    },
    RegisteredFixture(id: "native.messages.user-search.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.lingua_rs_detected_language",
                "results.profile_image_id",
                "results.public_verified_name_display",
                "results.verification_status",
                "results.verified_badge_visible",
                "results.verified_display_name"
            ]
        )
    }
]

let nativeUrlApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.hostnames.default") {
        try assertFixtureCoversDTO(
            $0,
            as: HostnamesResponse.self,
            ignoring: [
                "hostname_elections.hostname-1.__entity_type",
                "hostnames.hostname-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "topics.topic-1.__entity_type",
                "topics.topic-1.created_by",
                "topics.topic-1.created_by.id",
                "topics.topic-1.created_by.username",
                "topics.topic-1.hero_image_id",
                "topics.topic-1.homepage_url_id",
                "topics.topic-1.hostname",
                "topics.topic-1.lingua_rs_detected_language",
                "topics.topic-1.logo_image_id",
                "topics.topic-1.referral_program_id",
                "topics.topic-1.referral_program_slug",
                "topics.topic-1.rewards_program_id",
                "topics.topic-1.updated_by",
                "topics.topic-1.updated_by.id",
                "topics.topic-1.updated_by.username"
            ]
        )
    },
    RegisteredFixture(id: "native.hostname.default") {
        try assertFixtureCoversDTO(
            $0,
            as: HostnameResponse.self,
            ignoring: [
                "hostname.__entity_type",
                "hostname_election.__entity_type",
                "rss_feeds.__entity_type",
                "rss_feeds.etag",
                "rss_feeds.home_page_url",
                "rss_feeds.home_page_url.id",
                "rss_feeds.home_page_url.url",
                "rss_feeds.is_discoverable",
                "rss_feeds.is_enabled",
                "rss_feeds.last_fetched_at",
                "rss_feeds.last_modified_at",
                "rss_feeds.publisher_type",
                "rss_feeds.rss_feed_url.id",
                "rss_feeds.topic.__entity_type",
                "rss_feeds.topic.aliases",
                "rss_feeds.topic.allow_reviews",
                "rss_feeds.topic.created_at",
                "rss_feeds.topic.created_by",
                "rss_feeds.topic.created_by.id",
                "rss_feeds.topic.created_by.username",
                "rss_feeds.topic.hero_image_id",
                "rss_feeds.topic.homepage_url_id",
                "rss_feeds.topic.hostname",
                "rss_feeds.topic.hostname_id",
                "rss_feeds.topic.lingua_rs_detected_language",
                "rss_feeds.topic.logo_image_id",
                "rss_feeds.topic.markdown",
                "rss_feeds.topic.noindex",
                "rss_feeds.topic.referral_program_id",
                "rss_feeds.topic.referral_program_slug",
                "rss_feeds.topic.rewards_program_id",
                "rss_feeds.topic.updated_by",
                "rss_feeds.topic.updated_by.id",
                "rss_feeds.topic.updated_by.username",
                "topic.__entity_type",
                "topic.created_by",
                "topic.created_by.id",
                "topic.created_by.username",
                "topic.hero_image_id",
                "topic.homepage_url_id",
                "topic.hostname",
                "topic.lingua_rs_detected_language",
                "topic.logo_image_id",
                "topic.referral_program_id",
                "topic.referral_program_slug",
                "topic.rewards_program_id",
                "topic.updated_by",
                "topic.updated_by.id",
                "topic.updated_by.username"
            ]
        )
    },
    RegisteredFixture(id: "native.urls.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.canonical_url_id"
            ]
        )
    },
    RegisteredFixture(id: "native.url.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlResponse.self,
            ignoring: [
                "latest_crawl.__entity_type",
                "rss_feed_id",
                "url.__entity_type",
                "url.canonical_url_id",
                "url.hostname.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.url-crawls.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlCrawlsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.paid.url-crawls.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlCrawlsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor", "results.__entity_type"]
        )
    },
    RegisteredFixture(id: "native.url-crawl.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlCrawlResponse.self,
            ignoring: [
                "crawl.__entity_type",
                "og_image_sideload"
            ]
        )
    },
    RegisteredFixture(id: "native.paid.url-crawl.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UrlCrawlResponse.self,
            ignoring: ["crawl.__entity_type", "og_image_sideload"]
        )
    },
    RegisteredFixture(id: "web.paid.rss-feed-crawls.default") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedCrawlsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.paid.rss-feed-crawl.default") {
        try assertFixtureCoversDTO($0, as: RssFeedCrawlResponse.self)
    },
    RegisteredFixture(id: "native.url-crawl-trigger.default") {
        try assertFixtureCoversDTO($0, as: UrlCrawlTriggerResponse.self)
    }
]
