import VouchaAPI
import VouchaModels

private struct CommunityMutationResponse: Codable {
    let community: Community
}

let webCommunityApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "web.topics.mutation.default") {
        try assertFixtureCoversDTO($0, as: TopicEnvelope.self)
    },
    RegisteredFixture(id: "web.communities.archive.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityResponse.self,
            ignoring: [
                "community.__entity_type",
                "community.banner_image_id",
                "community.default_language",
                "community.deleted_at",
                "community.deleted_by_id",
                "community.lingua_rs_detected_language",
                "community.list_type",
                "community.member_invites_allowed_at",
                "community.post_approval_required_at",
                "community.profile_image_id",
                "community.rules_markdown",
                "community.trusted_at"
            ]
        )
    },
    RegisteredFixture(id: "web.communities.members.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityMembersResponse.self,
            ignoring: [
                "community_members.community-member-1.__entity_type",
                "community_members.community-member-1.approved_by_id",
                "community_members.community-member-1.removed_at",
                "community_members.community-member-1.removed_by_id",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "users.user-1.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "web.communities.posts.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.news.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.topics.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.rss-feeds.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.posts.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.domains.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.urls.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "web.communities.list-items.counts.default") {
        try assertFixtureCoversDTO($0, as: CommunityListItemCountsResponse.self)
    },
    RegisteredFixture(id: "web.communities.applications.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityApplicationsResponse.self,
            ignoring: ["automod_actions.feedback_label", "page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.application-questions.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityApplicationQuestionsResponse.self,
            ignoring: ["questions.options"]
        )
    },
    RegisteredFixture(id: "web.communities.pinned-posts.default") {
        try assertFixtureCoversDTO($0, as: CommunityPinnedPostsResponse.self)
    },
    RegisteredFixture(id: "web.communities.saved-replies.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunitySavedRepliesResponse.self,
            ignoring: ["automod_actions.feedback_label", "page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.saved-replies.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunitySavedRepliesResponse.self,
            ignoring: ["automod_actions.feedback_label", "page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.saved-reply.create.default") {
        try assertFixtureCoversDTO($0, as: CommunitySavedReplyResponse.self)
    },
    RegisteredFixture(id: "web.communities.invites.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityInvitesResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.bans.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityBansResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.restrictions.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityRestrictionsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor", "raid_mode_suggestion"]
        )
    },
    RegisteredFixture(id: "web.communities.modmail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModmailThreadsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.modmail.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModmailThreadsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.modmail-messages.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModmailMessagesResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.modmail-messages.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModmailMessagesResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.modlog.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModlogResponse.self,
            ignoring: [
                "moderator_actions.modlog-1.community_application_id",
                "moderator_actions.modlog-1.report_id",
                "moderator_actions.modlog-1.review_dispute_id",
                "moderator_actions.modlog-1.target_user_id",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "users.user-1.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "web.communities.moderator-stats.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModeratorStatsResponse.self,
            ignoring: ["users.user-1.profile_image_id"]
        )
    },
    RegisteredFixture(id: "web.communities.moderator-vacation.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ModeratorVacationResponse.self,
            ignoring: ["vacation.ends_at"]
        )
    },
    RegisteredFixture(id: "web.communities.moderation-queue.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModerationQueueResponse.self,
            ignoring: [
                "entries.admin_action_path",
                "entries.community_ban_evasion",
                "entries.flagged_reason",
                "entries.judgement",
                "entries.note",
                "entries.post_moderation_context",
                "entries.reporter_user_id",
                "entries.reporter_username",
                "entries.resolved_by_id",
                "entries.target_user_id",
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "web.communities.moderation-analytics.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModerationAnalytics.self,
            ignoring: [
                "appeals.success_rate",
                "automod_performance.false_positive_rate",
                "new_user_friction.rejection_rate"
            ]
        )
    },
    RegisteredFixture(id: "native.moderation.transparency.default") {
        try assertFixtureCoversDTO($0, as: ModerationTransparency.self)
    },
    RegisteredFixture(id: "native.community.moderation-transparency.default") {
        try assertFixtureCoversDTO($0, as: ModerationTransparency.self)
    },
    RegisteredFixture(id: "web.communities.ai-agents.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityAiAgentsResponse.self,
            ignoring: ["community_ai_agents.entitlement.reason"]
        )
    },
    RegisteredFixture(id: "web.communities.agent-prompts.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityAgentPromptsResponse.self,
            ignoring: [
                "community_agent_prompts.deactivated_at",
                "community_agent_prompts.deleted_at",
                "community_agent_prompts.deleted_by_id"
            ]
        )
    },
    RegisteredFixture(id: "web.communities.agent-prompts.history.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityAgentPromptHistoryResponse.self,
            ignoring: ["next_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.automod-simulate.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityAutomodSimulation.self,
            ignoring: ["simulation.false_positive_estimate"]
        )
    },
    RegisteredFixture(id: "web.communities.automod-recent-actions.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityAutomodRecentActionsResponse.self,
            ignoring: ["automod_actions.feedback_label", "page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "web.communities.automod-feedback.create.default") {
        try assertFixtureCoversDTO($0, as: CommunityAutomodFeedbackResponse.self)
    },
    RegisteredFixture(id: "web.communities.moderation-results.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityModerationResultsResponse.self,
            ignoring: []
        )
    },
    RegisteredFixture(id: "web.communities.warning.create.default") {
        try assertFixtureCoversDTO($0, as: CommunityWarningResponse.self, ignoring: ["warning.report_id"])
    },
    RegisteredFixture(id: "web.communities.post-type-settings.update.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CommunityMutationResponse.self,
            ignoring: [
                "community.__entity_type",
                "community.archived_at",
                "community.archived_by_id",
                "community.banner_image_id",
                "community.default_language",
                "community.deleted_at",
                "community.deleted_by_id",
                "community.lingua_rs_detected_language",
                "community.list_type",
                "community.member_invites_allowed_at",
                "community.post_approval_required_at",
                "community.profile_image_id",
                "community.rules_markdown",
                "community.trusted_at"
            ]
        )
    },
    RegisteredFixture(id: "web.topics.publisher-types.default") {
        try assertFixtureCoversDTO($0, as: PublisherTypesResponse.self)
    },
    RegisteredFixture(id: "native.topics.user-tags.default") {
        try assertFixtureCoversDTO($0, as: UserTagsResponse.self)
    },
    RegisteredFixture(id: "web.growth-metrics.default") {
        try assertFixtureCoversDTO($0, as: GrowthMetrics.self)
    }
]

let relationApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.entity-relations.post.category.topic.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-post-category-topic-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.post.related.post.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-post-related-post-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.post.related.url.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-post-related-url-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.topic.publisher-type.topic.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-topic-publisher-type-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.rss-feed-item.category.topic.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-rss-feed-item-category-topic-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.post.category.topic.create.default") {
        try assertFixtureCoversDTO($0, as: EntityRelationResponse.self)
    },
    RegisteredFixture(id: "native.entity-relations.user.category.topic.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationsResponse.self,
            ignoring: [
                "entity_relation_elections.relation-user-category-topic-bot.__entity_type",
                "entity_relations.relation-user-category-topic-bot.object_data.label",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type"
            ]
        )
    },
    RegisteredFixture(id: "native.entity-relations.user.category.topic.create.default") {
        try assertFixtureCoversDTO(
            $0,
            as: EntityRelationResponse.self,
            ignoring: ["relation.object_data.label"]
        )
    },
    RegisteredFixture(id: "native.entity-relations.post.category.topic.vote.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    }
]
