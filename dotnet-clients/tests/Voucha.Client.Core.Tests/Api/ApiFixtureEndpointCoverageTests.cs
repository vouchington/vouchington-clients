using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private const string SupportAdministratorId = "00000000-0000-7000-8000-000000000001";
  private const string SupportContactId = "00000000-0000-7000-8000-000000000711";
  private const string SupportThreadId = "00000000-0000-7000-8000-000000000712";
  private const string SupportMessageId = "00000000-0000-7000-8000-000000000713";
  private static readonly IReadOnlyDictionary<string, ApiRequest> Registry =
      WithOAuthBrokerEndpoints(
          WithRewardsProgramStatusEndpoints(
              WithAiCostEndpoints(
                  CreateCoreRegistry()
                      .Concat(CreateCrmAndAccountRegistry())
                      .Concat(CreateModerationParityRegistry())
                      .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal))));

  private static IReadOnlyDictionary<string, ApiRequest> CreateCoreRegistry() =>
      new Dictionary<string, ApiRequest>(StringComparer.Ordinal)
      {
        ["native.memberships.plans.default"] = VouchaApiEndpoints.MembershipPlans(),
        ["native.memberships.grant.default"] = VouchaApiEndpoints.GrantMembership(new GrantMembershipBody("00000000-0000-7000-8000-000000000003", MembershipGrantPlanSlug.Plus, "00000000-0000-7000-8000-000000000701")),
        ["native.identity-verification-attempts.grant.default"] = VouchaApiEndpoints.GrantIdentityVerificationAttempt(
            "00000000-0000-7000-8000-000000000003",
            new GrantIdentityVerificationAttemptBody("Provider terminal error reviewed by support.")),
        ["native.import-export.rss-feeds.submit.default"] = VouchaApiEndpoints.ImportRssFeedUrls(["https://example.test/feed.xml", "https://invalid.example.test/feed.xml"]),
        ["native.import-export.rss-feeds.status.retrying"] = VouchaApiEndpoints.RssFeedImportStatus("70000000-0000-7000-8000-000000000001"),
        ["native.import-export.rss-feeds.status.partial"] = VouchaApiEndpoints.RssFeedImportStatus("70000000-0000-7000-8000-000000000001"),
        ["native.import-export.topics.import.outcomes"] = VouchaApiEndpoints.ImportTopics(["Travel", "Local News", "Travel", "!!!"]),
        ["native.import-export.topics.export.default"] = VouchaApiEndpoints.ExportTopics(),
        ["native.feature-flags.default"] = VouchaApiEndpoints.FeatureFlags(),
        ["native.captcha-config.default"] = VouchaApiEndpoints.CaptchaConfig(),
        ["native.dynamic-config.namespaces.developer"] = VouchaApiEndpoints.DynamicConfigNamespaces(),
        ["native.dynamic-config.namespace.typed"] = VouchaApiEndpoints.DynamicConfigNamespace("recaptcha-config"),
        ["native.dynamic-config.namespace.string"] = VouchaApiEndpoints.DynamicConfigNamespace("app-attestation-config"),
        ["native.dynamic-config.namespace.integer"] = VouchaApiEndpoints.DynamicConfigNamespace("post-content-limits-config"),
        ["native.dynamic-config.update.changed"] = VouchaApiEndpoints.UpdateDynamicConfigField("feature-flags", "fediverse", DynamicConfigValues.From(true)),
        ["native.dynamic-config.update.no-op"] = VouchaApiEndpoints.UpdateDynamicConfigField("feature-flags", "fediverse", DynamicConfigValues.From(false)),
        ["native.dynamic-config.history.default"] = VouchaApiEndpoints.DynamicConfigHistory("feature-flags"),
        ["web.topics.search.default"] = VouchaApiEndpoints.SearchTopics("tech"),
        ["web.topics.mutation.default"] = VouchaApiEndpoints.CreateTopic(new CreateTopicRequest("Tech", "tech", "topic")),
        ["web.rss-feed-items.feed.default"] = VouchaApiEndpoints.RssFeedItems(limit: 25, mediaType: "article"),
        ["native.rss-feed-item.detail.default"] =
            VouchaApiEndpoints.RssFeedItem("00000000-0000-7000-8000-000000007886"),
        ["native.topic-recommendation.detail.default"] =
            VouchaApiEndpoints.TopicRecommendation("recommendation-1"),
        ["native.topic-recommendations.top-hashtags.default"] = VouchaApiEndpoints.TopHashtags(),
        ["web.topics.search.referral-programs.default"] = VouchaApiEndpoints.SearchTopics("test", "referral_program", 10),
        ["web.referral-links.feed.default"] = VouchaApiEndpoints.ReferralLinksFeed("follow_users"),
        ["native.referral-links.mine.default"] = VouchaApiEndpoints.ReferralLinks(),
        ["native.referral-clicks.mine.default"] = VouchaApiEndpoints.MyReferralClicks(),
        ["web.trending-referral-programs.default"] = VouchaApiEndpoints.TrendingReferralPrograms(),
        ["web.referral-links.prioritized.default"] = VouchaApiEndpoints.PrioritizedReferralLinks("referral-program-1", true),
        ["web.my.support-threads.create.default"] = VouchaApiEndpoints.CreateMySupportThread(
            new CreateSupportThreadBody("Account access issue", "I need help with my account.", null)),
        ["native.staff-support.threads.default"] = VouchaApiEndpoints.StaffSupportThreads("account", StaffSupportThreadStatusFilter.Open, limit: 25),
        ["native.staff-support.thread-detail.default"] = VouchaApiEndpoints.StaffSupportThread(SupportThreadId),
        ["native.staff-support.thread-assign.default"] = VouchaApiEndpoints.AssignStaffSupportThread(SupportThreadId, SupportAdministratorId),
        ["native.staff-support.thread-resolve.default"] = VouchaApiEndpoints.ResolveStaffSupportThread(SupportThreadId, true),
        ["native.staff-support.thread-reopen.default"] = VouchaApiEndpoints.ResolveStaffSupportThread(SupportThreadId, false),
        ["native.staff-support.messages.default"] = VouchaApiEndpoints.StaffSupportMessages(SupportThreadId),
        ["native.staff-support.message-create.default"] = VouchaApiEndpoints.CreateStaffSupportMessage(SupportThreadId, "Saved outbound reply."),
        ["native.staff-support.draft-create.default"] = VouchaApiEndpoints.QueueStaffSupportDraft(SupportThreadId),
        ["native.staff-support.message-edit.default"] = VouchaApiEndpoints.UpdateStaffSupportDraft(SupportThreadId, SupportMessageId, "Edited support draft."),
        ["native.staff-support.message-approve.default"] = VouchaApiEndpoints.ApproveStaffSupportMessage(SupportThreadId, SupportMessageId),
        ["native.staff-support.message-send.default"] = VouchaApiEndpoints.SendStaffSupportMessage(SupportThreadId, SupportMessageId),
        ["native.staff-support.contacts.default"] = VouchaApiEndpoints.StaffSupportContacts("traveler", limit: 25),
        ["native.staff-support.contact-detail.default"] = VouchaApiEndpoints.StaffSupportContact(SupportContactId, limit: 25),
        ["native.staff-support.contact-update.default"] = VouchaApiEndpoints.UpdateStaffSupportContact(SupportContactId, new("Traveler Support", "Updated support notes.")),
        ["web.admin.article-syncs.trigger.default"] = VouchaApiEndpoints.TriggerArticleSync(),
        ["web.admin.article-syncs.status.active"] = VouchaApiEndpoints.FetchArticleSyncStatus("job-1"),
        ["web.admin.mq.stats.default"] = VouchaApiEndpoints.FetchQueueStats(),
        ["web.admin.mq.queues.default"] = VouchaApiEndpoints.FetchQueues(),
        ["web.admin.mq.queues.pause.default"] = VouchaApiEndpoints.PauseQueue("psql"),
        ["web.admin.mq.queues.resume.default"] = VouchaApiEndpoints.ResumeQueue("psql"),
        ["web.admin.mq.scheduled-jobs.default"] = VouchaApiEndpoints.FetchScheduledJobs(),
        ["web.admin.mq.scheduled-jobs.trigger.default"] = VouchaApiEndpoints.TriggerScheduledJob("kagi-smallweb-sync"),
        ["web.admin.mq.backfills.default"] = VouchaApiEndpoints.FetchBackfills(),
        ["web.admin.mq.backfills.trigger.default"] = VouchaApiEndpoints.TriggerBackfill("openai-moderation-posts"),
        ["web.admin.psql.migrations.default"] = VouchaApiEndpoints.FetchPsqlMigrations(),
        ["web.admin.psql.partitions.default"] = VouchaApiEndpoints.FetchPsqlPartitions(),
        ["web.admin.psql.jobs.default"] = VouchaApiEndpoints.EnqueuePsqlJob(new PsqlJobBody("runConfigDriven")),
        ["web.admin.valkey.bloom-filters.rebuild.default"] =
            VouchaApiEndpoints.RebuildBloomFilter(new RebuildBloomFilterBody("entity-cache")),
        ["web.admin.valkey.cache-groups.default"] = VouchaApiEndpoints.FetchCacheGroups(),
        ["web.admin.valkey.caches.clear.default"] = VouchaApiEndpoints.ClearCache(new ClearCacheBody("posts")),
        ["web.admin.valkey.flush.default"] = VouchaApiEndpoints.FlushValkey(new FlushValkeyBody("blooms")),
        ["web.communities.search.default"] = VouchaApiEndpoints.SearchCommunities("test"),
        ["web.communities.show.default"] = VouchaApiEndpoints.ShowCommunity("test-community"),
        ["web.communities.ai-agent.default"] = VouchaApiEndpoints.EnableCommunityAiAgent("test-community", "self-promotion"),
        ["web.communities.archive.default"] = VouchaApiEndpoints.UpdateCommunity("test-community", ArchiveCommunityBody()),
        ["web.communities.members.default"] = VouchaApiEndpoints.CommunityMembers("test-community", limit: 25),
        ["web.communities.posts.default"] = VouchaApiEndpoints.CommunityPosts("test-community", limit: 25),
        ["web.communities.news.default"] = VouchaApiEndpoints.CommunityNews("test-community"),
        ["web.communities.list-items.topics.default"] = VouchaApiEndpoints.CommunityListTopics("test-community"),
        ["web.communities.list-items.rss-feeds.default"] = VouchaApiEndpoints.CommunityListRssFeeds("test-community"),
        ["web.communities.list-items.posts.default"] = VouchaApiEndpoints.CommunityListPosts("test-community"),
        ["web.communities.list-items.domains.default"] = VouchaApiEndpoints.CommunityListDomains("test-community"),
        ["web.communities.list-items.urls.default"] = VouchaApiEndpoints.CommunityListUrls("test-community"),
        ["web.communities.list-items.counts.default"] = VouchaApiEndpoints.CommunityListItemCounts("test-community"),
        ["web.communities.application-questions.default"] = VouchaApiEndpoints.CommunityApplicationQuestions("test-community"),
        ["web.communities.pinned-posts.default"] = VouchaApiEndpoints.CommunityPinnedPosts("test-community"),
        ["web.communities.saved-replies.default"] = VouchaApiEndpoints.CommunitySavedReplies("test-community"),
        ["web.communities.saved-replies.page-2"] = VouchaApiEndpoints.CommunitySavedReplies(
            "test-community",
            "eyJyYW5raW5nIjowLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcwMSJ9",
            25),
        ["web.communities.saved-reply.create.default"] = VouchaApiEndpoints.CreateCommunitySavedReply(
            "test-community",
            new CreateCommunitySavedReplyRequest("Greeting", "Thanks for writing in.")),
        ["web.communities.applications.default"] = VouchaApiEndpoints.CommunityApplications("test-community", limit: 25),
        ["web.communities.invites.default"] = VouchaApiEndpoints.CommunityInvites("test-community", limit: 25),
        ["web.communities.bans.default"] = VouchaApiEndpoints.CommunityBans("test-community"),
        ["web.communities.restrictions.default"] = VouchaApiEndpoints.CommunityRestrictions("test-community"),
        ["web.communities.modmail.default"] = VouchaApiEndpoints.CommunityModmail("test-community"),
        ["web.communities.modmail.page-2"] = VouchaApiEndpoints.CommunityModmail(
            "test-community",
            "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDEyOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDUwMSJ9",
            25),
        ["web.communities.modmail-messages.default"] = VouchaApiEndpoints.CommunityModmailMessages(
            "test-community",
            "00000000-0000-7000-8000-000000000501"),
        ["web.communities.modmail-messages.page-2"] = VouchaApiEndpoints.CommunityModmailMessages(
            "test-community",
            "00000000-0000-7000-8000-000000000501",
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDYwMSJ9",
            25),
        ["web.communities.modlog.default"] = VouchaApiEndpoints.CommunityModlog("test-community"),
        ["web.communities.moderation-queue.default"] = VouchaApiEndpoints.CommunityModerationQueue("test-community"),
        ["native.community.pending-reports.paginated"] = VouchaApiEndpoints.CommunityPendingReports(
            "fixture-community",
            "fixture-community-role-and-sort-scoped-report-cursor",
            1),
        ["web.communities.moderation-analytics.default"] = VouchaApiEndpoints.CommunityModerationAnalytics("test-community"),
        ["web.communities.moderator-stats.default"] = VouchaApiEndpoints.CommunityModeratorStats("test-community"),
        ["web.communities.moderator-vacation.default"] = VouchaApiEndpoints.CommunityModeratorVacation("test-community"),
        ["native.moderation.reports.default"] = VouchaApiEndpoints.ModerationReports(),
        ["native.moderation.reports.member.default"] = VouchaApiEndpoints.ModerationReports(),
        ["native.moderation.reports.clustered.default"] = VouchaApiEndpoints.ClusteredModerationReports(limit: 4),
        ["native.moderation.reports.clustered.page-2"] = VouchaApiEndpoints.ClusteredModerationReports(
            after: "eyJjbHVzdGVyIjp0cnVlLCJjcmVhdGVkX2F0IjoiMjAyNi0wNi0wMVQxMjowNTowMC4wMDAwMDBaIiwiZW50aXR5X3R5cGUiOiJwb3N0IiwiaWQiOiIwMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAxMDMiLCJzb3J0IjoiY3JlYXRlZF9hdF9kZXNjIiwic3RhdHVzIjoicGVuZGluZyIsInNjb3BlIjoie1wiYXVkaWVuY2VcIjpcInN0YWZmXCIsXCJvd25lcklkXCI6bnVsbH0ifQ",
            limit: 3),
        ["native.moderation.report-judgement.default"] = VouchaApiEndpoints.RerunModerationReportJudgement("019f6559-6b31-7171-b5b1-ccd9d702c45e"),
        ["native.moderation.report-resolution.reviewed"] = VouchaApiEndpoints.ResolveModerationReport("019f6559-6b31-7171-b5b1-ccd9d702c45e", ModerationReportResolution.Reviewed),
        ["native.moderation.admin-warning.report"] = VouchaApiEndpoints.IssueAdminWarning(
            new IssueAdminWarningRequest("user-2", "Repeated harassment", "Stop contacting this user.", "019f6559-6b31-7171-b5b1-ccd9d702c45e")),
        ["native.moderation.ban-evasion.confirm"] = VouchaApiEndpoints.ConfirmReportBanEvasion("community-1", "user-2"),
        ["native.moderation.ban-evasion.dismiss"] = VouchaApiEndpoints.DismissReportBanEvasion("community-1", "user-2"),
        ["native.moderation.appeals.default"] = VouchaApiEndpoints.Appeals(ModerationAppealStatus.Pending),
        ["native.moderation.appeals.page-2"] = VouchaApiEndpoints.Appeals(
            ModerationAppealStatus.Pending,
            after: ApiFixtureLoader.QueryValue("native.moderation.appeals.page-2", "after")),
        ["native.moderation.appeals.update.default"] = VouchaApiEndpoints.UpdateAppeal(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.update.default", "id"),
            publicResponse: "We reviewed your appeal and reduced the action."),
        ["native.moderation.appeals.approval.default"] = VouchaApiEndpoints.AppealApproval(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.approval.default", "id")),
        ["native.moderation.appeals.delivery.default"] = VouchaApiEndpoints.AppealDelivery(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.delivery.default", "id")),
        ["native.moderation.appeals.resolution.accept"] = VouchaApiEndpoints.AppealResolution(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.resolution.accept", "id"),
            ModerationAppealAction.Accept),
        ["native.moderation.appeals.resolution.reduce"] = VouchaApiEndpoints.AppealResolution(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.resolution.reduce", "id"),
            ModerationAppealAction.Reduce),
        ["native.moderation.appeals.resolution.deny"] = VouchaApiEndpoints.AppealResolution(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.resolution.deny", "id"),
            ModerationAppealAction.Deny),
        ["native.moderation.appeals.resolution-drafts.default"] = VouchaApiEndpoints.AppealResolutionDrafts(
            ApiFixtureLoader.RouteParameterValue("native.moderation.appeals.resolution-drafts.default", "id")),
        ["native.moderation.disputes.default"] = VouchaApiEndpoints.Disputes("pending"),
        ["native.moderation.review-queue.default"] = VouchaApiEndpoints.AdminReviewQueue(limit: 2),
        ["native.moderation.review-queue.page-2"] = VouchaApiEndpoints.AdminReviewQueue(ReviewQueueFixtureConstants.PageOneEndCursor, 2),
        ["native.moderation.clearance.approved"] = VouchaApiEndpoints.UpdatePostClearance(ReviewQueueFixtureConstants.InReviewPostId, PostClearanceAction.Approved),
        ["native.moderation.clearance.rejected"] = VouchaApiEndpoints.UpdatePostClearance(ReviewQueueFixtureConstants.InReviewPostId, PostClearanceAction.Rejected),
        ["native.moderation.clearance.in-review"] = VouchaApiEndpoints.UpdatePostClearance(ReviewQueueFixtureConstants.RejectedPostId, PostClearanceAction.InReview),
        ["native.moderation.modlog.default"] = VouchaApiEndpoints.AdminModlog(),
        ["native.moderation.analytics.default"] = VouchaApiEndpoints.AdminModerationAnalytics(),
        ["native.moderation.vote-integrity.default"] = VouchaApiEndpoints.VoteIntegrityFlags(),
        ["native.moderation.vote-integrity.pending"] = VouchaApiEndpoints.VoteIntegrityFlags(IntegrityFlagStatus.Pending),
        ["native.moderation.vote-integrity.resolved"] = VouchaApiEndpoints.VoteIntegrityFlags(IntegrityFlagStatus.Resolved),
        ["native.moderation.vote-integrity.resolution.dismissed"] = VouchaApiEndpoints.ResolveVoteIntegrityFlag("vote-flag-1", VoteIntegrityResolution.Dismissed),
        ["native.moderation.vote-integrity.resolution.penalized"] = VouchaApiEndpoints.ResolveVoteIntegrityFlag("vote-flag-1", VoteIntegrityResolution.Penalized),
        ["native.moderation.vote-integrity.resolution.suspended"] = VouchaApiEndpoints.ResolveVoteIntegrityFlag("vote-flag-1", VoteIntegrityResolution.Suspended),
        ["native.moderation.vote-integrity.penalty"] = VouchaApiEndpoints.ApplyVoteIntegrityPenalty("vote-flag-1"),
        ["native.moderation.vote-integrity.penalties.default"] = VouchaApiEndpoints.VoteIntegrityPenalties(),
        ["native.moderation.vote-integrity.penalties.active"] = VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.Active),
        ["native.moderation.vote-integrity.penalties.revoked"] = VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.Revoked),
        ["native.moderation.vote-integrity.penalties.all"] = VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.All),
        ["native.moderation.vote-integrity.penalties.page-2"] = VouchaApiEndpoints.VoteIntegrityPenalties(IntegrityPenaltyStatus.Active, IntegrityPenaltyFixtureConstants.VotePageOneEndCursor),
        ["native.moderation.vote-integrity.penalties.get"] = VouchaApiEndpoints.VoteIntegrityPenalty("019f7000-0000-7000-8000-000000000201"),
        ["native.moderation.vote-integrity.penalties.revoke"] = VouchaApiEndpoints.RevokeVoteIntegrityPenalty("019f7000-0000-7000-8000-000000000201"),
        ["native.moderation.report-integrity.default"] = VouchaApiEndpoints.ReportIntegrityFlags(),
        ["native.moderation.report-integrity.pending"] = VouchaApiEndpoints.ReportIntegrityFlags(IntegrityFlagStatus.Pending),
        ["native.moderation.report-integrity.resolved"] = VouchaApiEndpoints.ReportIntegrityFlags(IntegrityFlagStatus.Resolved),
        ["native.moderation.report-integrity.resolution.dismissed"] = VouchaApiEndpoints.ResolveReportIntegrityFlag("report-flag-1", ReportIntegrityPatchResolution.Dismissed),
        ["native.moderation.report-integrity.penalty"] = VouchaApiEndpoints.ApplyReportIntegrityPenalty("report-flag-1"),
        ["native.moderation.report-integrity.penalties.default"] = VouchaApiEndpoints.ReportIntegrityPenalties(),
        ["native.moderation.report-integrity.penalties.active"] = VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.Active),
        ["native.moderation.report-integrity.penalties.revoked"] = VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.Revoked),
        ["native.moderation.report-integrity.penalties.all"] = VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.All),
        ["native.moderation.report-integrity.penalties.page-2"] = VouchaApiEndpoints.ReportIntegrityPenalties(IntegrityPenaltyStatus.Active, IntegrityPenaltyFixtureConstants.ReportPageOneEndCursor),
        ["native.moderation.report-integrity.penalties.get"] = VouchaApiEndpoints.ReportIntegrityPenalty("019f7000-0000-7000-8000-000000000101"),
        ["native.moderation.report-integrity.penalties.revoke"] = VouchaApiEndpoints.RevokeReportIntegrityPenalty("019f7000-0000-7000-8000-000000000101"),
        ["web.communities.ai-agents.default"] = VouchaApiEndpoints.CommunityAiAgents("test-community"),
        ["web.communities.agent-prompts.default"] = VouchaApiEndpoints.CommunityAgentPrompts("test-community"),
        ["web.communities.agent-prompts.history.default"] = VouchaApiEndpoints.CommunityAgentPromptHistory("test-community"),
        ["web.communities.automod-simulate.default"] = VouchaApiEndpoints.SimulateCommunityAutomod("test-community", CommunityAutomodSimulationFixtureBody()),
        ["web.communities.automod-recent-actions.default"] =
            VouchaApiEndpoints.CommunityAutomodRecentActions("test-community", limit: 25, window: "24h", source: "agent_moderation"),
        ["web.communities.automod-feedback.create.default"] = VouchaApiEndpoints.RecordCommunityAutomodFeedback(
            "test-community",
            "agent_moderation:post-1",
            new CommunityAutomodFeedbackRequest("true_positive", "keep_removed", "correct", "Matches the community rules.")),
        ["web.communities.moderation-results.default"] = VouchaApiEndpoints.CommunityModerationResults("test-community", "post-1"),
        ["web.communities.warning.create.default"] = VouchaApiEndpoints.IssueCommunityWarning(
            "test-community",
            new IssueCommunityWarningRequest(
                "user-1",
                "Spam in community",
                "Please read the community rules.",
                ResolveReport: false)),
        ["web.communities.post-type-settings.update.default"] = VouchaApiEndpoints.UpdateCommunityPostTypeSettings(
            "test-community",
            new UpdateCommunityPostTypeSettingsRequest(true, true)),
        ["web.growth-metrics.default"] = VouchaApiEndpoints.GrowthMetrics(GrowthMetricsRange.ThirtyDays),
        ["shared.currencies.list.default"] = VouchaApiEndpoints.Currencies(),
        ["web.topics.publisher-types.default"] = VouchaApiEndpoints.PublisherTypes(),
        ["native.topics.user-tags.default"] = VouchaApiEndpoints.UserTags(),
        ["native.entity-relations.post.category.topic.default"] =
            VouchaApiEndpoints.EntityRelations(
                "post",
                "post-1",
                "category",
                "topic",
                limit: 1,
                sort: "best",
                after: "fixture-relation-scope-and-sort-cursor"),
        ["native.entity-relations.post.related.post.default"] =
            VouchaApiEndpoints.EntityRelations("post", "post-1", "related", "post"),
        ["native.entity-relations.post.related.url.default"] =
            VouchaApiEndpoints.EntityRelations("post", "post-1", "related", "url"),
        ["native.entity-relations.topic.publisher-type.topic.default"] =
            VouchaApiEndpoints.EntityRelations("topic", "topic-1", "publisher_type", "topic"),
        ["native.entity-relations.rss-feed-item.category.topic.default"] =
            VouchaApiEndpoints.EntityRelations("rss_feed_item", "item-1", "category", "topic"),
        ["native.entity-relations.post.category.topic.create.default"] =
            VouchaApiEndpoints.CreateEntityRelation("post", "post-1", "category", "topic", CreateEntityRelationFixtureBody()),
        ["native.entity-relations.user.category.topic.default"] =
            VouchaApiEndpoints.EntityRelations("user", "user-abc", "category", "topic", positiveNetVoteScore: true),
        ["native.entity-relations.user.category.topic.create.default"] =
            VouchaApiEndpoints.CreateEntityRelation("user", "user-abc", "category", "topic", new CreateEntityRelationBody("user-tag-bot")),
        ["native.entity-relations.post.category.topic.vote.default"] =
            VouchaApiEndpoints.VoteEntityRelation("relation-post-category-topic-1", ElectionVoteChoice.Confirm),
        ["native.bookmarks.posts.saved.default"] = VouchaApiEndpoints.UserPosts("user-abc", "saved"),
        ["native.bookmarks.posts.saved.next-page"] = VouchaApiEndpoints.UserPosts(
            "user-abc", "saved", after: BookmarkFixtureConstants.SavedPostsPageOneEndCursor),
        ["native.bookmarks.topics.muted.default"] = VouchaApiEndpoints.UserTopics("user-abc", "muted"),
        ["native.bookmarks.topics.viewed.default"] = VouchaApiEndpoints.UserTopics("user-abc", "viewed"),
        ["native.bookmarks.users.subscribed-posts.default"] = VouchaApiEndpoints.UserUsers("user-abc", "subscribed-posts"),
        ["native.bookmarks.users.dismissed-recommendations.default"] =
            VouchaApiEndpoints.UserUsers("user-abc", "dismissed-recommendations"),
        ["native.bookmarks.rss-feed-items.saved.default"] = VouchaApiEndpoints.UserRssFeedItems("user-abc", "saved", "article"),
        ["native.bookmarks.rss-feeds.muted.default"] =
            VouchaApiEndpoints.UserRssFeeds("user-abc", "muted", feedType: "article"),
        ["native.bookmarks.rss-feeds.viewed.default"] =
            VouchaApiEndpoints.UserRssFeeds("user-abc", "viewed", feedType: "podcast"),
        ["native.bookmarks.urls.saved.default"] = VouchaApiEndpoints.UserUrls("user-abc", "saved"),
        ["native.bookmarks.domains.blocked.default"] = VouchaApiEndpoints.UserHostnames("user-abc", "blocked"),
        ["native.bookmarks.domains.muted.default"] = VouchaApiEndpoints.UserHostnames("user-abc", "muted"),
        ["native.bookmarks.communities.proxy-following.default"] =
            VouchaApiEndpoints.UserCommunities("user-abc", "proxy-following"),
        ["native.lists-containing.default"] = VouchaApiEndpoints.ListsContaining("rss_feed_item", "item-1"),
        ["native.list-import.default"] = VouchaApiEndpoints.ImportCommunityList("list-1", ImportCommunityListFixtureBody()),
        ["native.comments.post-detail.default"] = VouchaApiEndpoints.Post("comment-root"),
        ["native.comments.descendants.default"] = VouchaApiEndpoints.PostDescendants(
            "comment-root-post",
            "fixture-root-and-subtree-scoped-cursor",
            2),
        ["native.comments.ancestors.permalink"] = VouchaApiEndpoints.PostAncestors("comment-b"),
        ["native.lists.default"] = VouchaApiEndpoints.Lists(),
        ["native.list-items.default"] = VouchaApiEndpoints.ListItems("list-1"),
        ["native.landing-pages.default"] = VouchaApiEndpoints.MyLandingPages(),
        ["native.landing-page-detail.default"] = VouchaApiEndpoints.MyLandingPage("landing-page-1"),
        ["native.landing-page-analytics.default"] = VouchaApiEndpoints.MyLandingPageAnalytics("landing-page-1"),
        ["native.admin-user-landing-pages.default"] = VouchaApiEndpoints.AdminUserLandingPages("user-abc"),
        ["native.admin-landing-page-analytics.default"] = VouchaApiEndpoints.AdminLandingPageAnalytics("landing-page-1"),
        ["native.landing-page-candidates.default"] = VouchaApiEndpoints.MyLandingPageCandidates(),
        ["native.landing-page-items-mutation.default"] = ApiFixtureLandingPageItems.Request(),
        ["native.landing-page-mutation.default"] = VouchaApiEndpoints.UpdateMyLandingPage("landing-page-1", LandingPageUpdateBody()),
        ["native.households.empty"] = VouchaApiEndpoints.Households(),
        ["native.households.single-owned"] = VouchaApiEndpoints.Households(),
        ["native.households.multiple"] = VouchaApiEndpoints.Households(),
        ["native.households.owned"] = VouchaApiEndpoints.Households(HouseholdAccess.Owned, limit: 1),
        ["native.households.member.default"] = VouchaApiEndpoints.Households(HouseholdAccess.Member, limit: 1),
        ["native.households.member.page-2"] = VouchaApiEndpoints.Households(
            HouseholdAccess.Member,
            "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDAwOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoiaG91c2Vob2xkczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAwMDE6YWNjZXNzPW1lbWJlcjp1cGRhdGVkX2F0LWRlc2MsaWQtZGVzYyJ9",
            1),
        ["native.household-memberships.empty"] = VouchaApiEndpoints.HouseholdMemberships(
            "00000000-0000-7000-8000-000000000101"),
        ["native.household-memberships.single"] = VouchaApiEndpoints.HouseholdMemberships(
            "00000000-0000-7000-8000-000000000101"),
        ["native.household-memberships.multiple"] = VouchaApiEndpoints.HouseholdMemberships(
            "00000000-0000-7000-8000-000000000101"),
        ["native.household-memberships.page-1"] = VouchaApiEndpoints.HouseholdMemberships(
            "00000000-0000-7000-8000-000000000101",
            limit: 1),
        ["native.household-memberships.page-2"] = VouchaApiEndpoints.HouseholdMemberships(
            "00000000-0000-7000-8000-000000000101",
            "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAyVDAwOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoiaG91c2Vob2xkLW1lbWJlcnNoaXBzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMTp1cGRhdGVkX2F0LWRlc2MsaWQtZGVzYyJ9",
            1),
        ["native.households.create.default"] = VouchaApiEndpoints.CreateHousehold(),
        ["native.household-memberships.delete.default"] = VouchaApiEndpoints.DeleteHouseholdMembership(
            "00000000-0000-7000-8000-000000000101",
            "00000000-0000-7000-8000-000000000201"),
        ["native.card-topics.search.default"] = VouchaApiEndpoints.PaymentCardTopics("Freedom"),
        ["native.cards.empty"] = VouchaApiEndpoints.PaymentCards(),
        ["native.cards.page-1"] = VouchaApiEndpoints.PaymentCards(limit: 2),
        ["native.cards.page-2"] = VouchaApiEndpoints.PaymentCards(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcwMiIsInNjb3BlIjoibXktY2FyZHM6MDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwNzAwOmlkLWFzYyJ9", 2),
        ["native.cards.create.default"] = VouchaApiEndpoints.CreatePaymentCard(
            new CreatePaymentCardBody("00000000-0000-7000-8000-000000000713")),
        ["native.cards.update.full"] = VouchaApiEndpoints.UpdatePaymentCard(
            "00000000-0000-7000-8000-000000000701",
            new UpdatePaymentCardBody(
                JsonNullableDate.FromDate(new DateOnly(2024, 1, 20)), JsonNullableDate.Null,
                JsonNullableDate.Null, JsonNullableMoney.FromMoney(new Money(0, "usd")), true,
                JsonNullableString.FromString("00000000-0000-7000-8000-000000000703"),
                JsonNullableString.FromString("Authorized-user account"))),
        ["native.cards.update.clear"] = VouchaApiEndpoints.UpdatePaymentCard(
            "00000000-0000-7000-8000-000000000701",
            new UpdatePaymentCardBody(
                JsonNullableDate.Null, JsonNullableDate.Null, JsonNullableDate.Null,
                JsonNullableMoney.Null, false, JsonNullableString.Null, JsonNullableString.Null)),
        ["native.cards.delete.default"] = VouchaApiEndpoints.DeletePaymentCard(
            "00000000-0000-7000-8000-000000000701"),
        ["native.rewards-program-topics.search.default"] = VouchaApiEndpoints.RewardsProgramTopics("Travel"),
        ["native.point-valuations.empty"] = VouchaApiEndpoints.PointValuations(),
        ["native.point-valuations.page-1"] = VouchaApiEndpoints.PointValuations(limit: 2),
        ["native.point-valuations.page-2"] = VouchaApiEndpoints.PointValuations(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcyMiIsInNjb3BlIjoibXktcG9pbnQtdmFsdWF0aW9uczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3MjA6aWQtYXNjIn0",
            2),
        ["native.point-valuations.create.default"] = VouchaApiEndpoints.CreatePointValuation(
            new CreatePointValuationBody(
                "00000000-0000-7000-8000-000000000731", new ScaledMoney(35_000, "usd"),
                "Use for flexible travel redemptions")),
        ["native.point-valuations.update.full"] = VouchaApiEndpoints.UpdatePointValuation(
            "00000000-0000-7000-8000-000000000721",
            new UpdatePointValuationBody(
                new ScaledMoney(35_000, "usd"),
                JsonNullableString.FromString("Use for flexible travel redemptions"))),
        ["native.point-valuations.update.clear-note"] = VouchaApiEndpoints.UpdatePointValuation(
            "00000000-0000-7000-8000-000000000722",
            new UpdatePointValuationBody(new ScaledMoney(0, "usd"), JsonNullableString.Null)),
        ["native.point-valuations.delete.default"] = VouchaApiEndpoints.DeletePointValuation(
            "00000000-0000-7000-8000-000000000721"),
        ["native.spending-categories.empty"] = VouchaApiEndpoints.SpendingCategories(),
        ["native.spending-categories.page-1"] = VouchaApiEndpoints.SpendingCategories(limit: 1),
        ["native.spending-categories.owned-household"] = VouchaApiEndpoints.SpendingCategories(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MiIsInNjb3BlIjoibXktc3BlbmRpbmctY2F0ZWdvcmllczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3NDA6aWQtYXNjIn0", 1),
        ["native.spending-categories.page-2"] = VouchaApiEndpoints.SpendingCategories(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MyIsInNjb3BlIjoibXktc3BlbmRpbmctY2F0ZWdvcmllczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3NDA6aWQtYXNjIn0", 1),
        ["native.spending-categories.create.default"] = VouchaApiEndpoints.CreateSpendingCategory(
            new CreateSpendingCategoryBody(
                "00000000-0000-7000-8000-000000000741",
                new Money(42_550, "usd"),
                "monthly",
                "Family groceries")),
        ["native.spending-categories.update.clear-note"] = VouchaApiEndpoints.UpdateSpendingCategory(
            "00000000-0000-7000-8000-000000000742",
            new UpdateSpendingCategoryBody(
                Amount: new Money(42_550, "usd"),
                Note: JsonNullableString.Null)),
        ["native.spending-categories.delete.default"] = VouchaApiEndpoints.DeleteSpendingCategory(
            "00000000-0000-7000-8000-000000000742"),
        ["native.spending-category-topics.search.default"] = VouchaApiEndpoints.SpendingCategoryTopics("Groceries"),
        ["native.users.profile.default"] = VouchaApiEndpoints.User("alice", true),
        ["native.users.profile.restricted"] = VouchaApiEndpoints.User("restricted", true),
        ["native.users.vouch-context.default"] = VouchaApiEndpoints.UserTrustContext("user-abc"),
        ["native.users.profile.topics-following.first-page"] = VouchaApiEndpoints.UserTopics("user-abc", "following"),
        ["native.users.profile.topics-following.next-page"] = VouchaApiEndpoints.UserTopics("user-abc", "following", "eyJ0aW1lc3RhbXAiOjE3ODI5MjE2MDAwMDAwMDAsImlkIjoiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMDAxIn0"),
        ["native.users.profile.sources-following.article"] = VouchaApiEndpoints.UserRssFeeds("user-abc", feedType: "article"),
        ["native.users.profile.communities-member.first-page"] = VouchaApiEndpoints.UserCommunities("user-abc", "member"),
        ["native.users.profile.communities-member.next-page"] = VouchaApiEndpoints.UserCommunities("user-abc", "member", "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9"),
        ["native.users.delete.default"] = VouchaApiEndpoints.DeleteUser("user-abc"),
        ["native.users.data-request.default"] = VouchaApiEndpoints.UserDataRequest("user-abc"),
        ["native.users.data-request.create.default"] = VouchaApiEndpoints.CreateUserDataRequest("user-abc"),
        ["native.hostnames.default"] = VouchaApiEndpoints.Hostnames(query: "example"),
        ["native.hostname.default"] = VouchaApiEndpoints.Hostname("hostname-1"),
        ["native.urls.default"] = VouchaApiEndpoints.Urls(query: "example"),
        ["native.url.default"] = VouchaApiEndpoints.Url("url-1"),
        ["native.url-crawls.default"] = VouchaApiEndpoints.UrlCrawls("url-1"),
        ["native.paid.url-crawls.default"] = VouchaApiEndpoints.UrlCrawls("url-1"),
        ["native.url-crawl.default"] = VouchaApiEndpoints.UrlCrawl("url-1", "crawl-1"),
        ["native.paid.url-crawl.default"] = VouchaApiEndpoints.UrlCrawl("url-1", "crawl-1"),
        ["web.paid.rss-feed-crawls.default"] = VouchaApiEndpoints.RssFeedCrawls("rss-feed-1"),
        ["web.paid.rss-feed-crawl.default"] = VouchaApiEndpoints.RssFeedCrawl("rss-feed-1", "crawl-1"),
        ["web.admin.rss-feed-crawl.default"] = VouchaApiEndpoints.RssFeedCrawl("rss-feed-1", "crawl-1"),
        ["native.url-crawl-trigger.default"] = VouchaApiEndpoints.TriggerUrlCrawl("url-1"),
        ["native.messages.conversations.default"] = VouchaApiEndpoints.MyMessages(),
        ["native.agents.default"] = VouchaApiEndpoints.Agents(limit: 2),
        ["native.agents.page-2"] = VouchaApiEndpoints.Agents(
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDMwMSIsInNjb3BlIjoiYWdlbnQtZGlyZWN0b3J5OmlkLWRlc2MifQ",
            2),
        ["native.agents.detail.default"] = VouchaApiEndpoints.Agent("helper"),
        ["native.agents.conversations.default"] = VouchaApiEndpoints.AgentConversations(
            "helper",
            limit: 2),
        ["native.agents.conversations.filtered-username"] = VouchaApiEndpoints.AgentConversations(
            "helper", limit: 2, filter: new AgentConversationFilter(AgentConversationFilterKind.Username, "fixture-agent-user-011")),
        ["native.agents.conversations.page-2"] = VouchaApiEndpoints.AgentConversations(
            "helper",
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMTAxMSIsInNjb3BlIjoie1wiYWdlbnRTeXN0ZW1Vc2VySWRcIjpcIjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMVwiLFwidXNlcklkXCI6bnVsbCxcInBvc3RJZFwiOm51bGwsXCJyc3NGZWVkSXRlbUlkXCI6bnVsbCxcIm9ubHlMaW5rZWRcIjp0cnVlLFwib3JkZXJcIjpcImlkLWRlc2NcIn0ifQ",
            2),
        ["native.agents.conversation.default"] = VouchaApiEndpoints.AgentConversation(
            "helper",
            "00000000-0000-7000-8000-000000000101",
            limit: 2),
        ["native.agents.conversation.page-2"] = VouchaApiEndpoints.AgentConversation(
            "helper",
            "00000000-0000-7000-8000-000000000101",
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoie1wiYWdlbnRJZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMzAyXCIsXCJjb252ZXJzYXRpb25JZFwiOlwiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMTAxXCIsXCJvcmRlclwiOlwiaWQtZGVzY1wifSJ9",
            2),
        ["native.messages.conversations.page-2"] = VouchaApiEndpoints.MyMessages(
            "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDA5OjE1OjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiJ9"),
        ["native.messages.conversation.default"] = VouchaApiEndpoints.MyMessage("00000000-0000-7000-8000-000000000101"),
        ["native.messages.create.default"] = VouchaApiEndpoints.CreateMyMessages(
            new CreateDirectConversationBody(["00000000-0000-7000-8000-000000000002"])),
        ["native.messages.thread.default"] = VouchaApiEndpoints.MyMessageConversationMessages("00000000-0000-7000-8000-000000000101"),
        ["native.messages.thread.page-2"] = VouchaApiEndpoints.MyMessageConversationMessages(
            "00000000-0000-7000-8000-000000000101",
            "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSJ9",
            50),
        ["native.messages.send.default"] = VouchaApiEndpoints.CreateMyMessageMessage(
            "00000000-0000-7000-8000-000000000101",
            new SendDirectMessageBody("Sent from native")),
        ["native.messages.participants.default"] =
            VouchaApiEndpoints.MyMessageConversationParticipants("00000000-0000-7000-8000-000000000101"),
        ["native.messages.participant-add.default"] = VouchaApiEndpoints.AddMyMessageConversationParticipant(
            "00000000-0000-7000-8000-000000000101",
            new AddDirectConversationParticipantBody("00000000-0000-7000-8000-000000000003")),
        ["native.messages.participant-remove.default"] = VouchaApiEndpoints.RemoveMyMessageConversationParticipant(
            "00000000-0000-7000-8000-000000000101",
            "00000000-0000-7000-8000-000000000002"),
        ["native.messages.policy.default"] = VouchaApiEndpoints.UpdateMyMessageConversationParticipantPolicy(
            "00000000-0000-7000-8000-000000000101",
            new UpdateDirectConversationParticipantPolicyBody("owner_only")),
        ["native.messages.user-search.default"] = VouchaApiEndpoints.SearchUsers("bo", limit: 10),
      };

  private static UpdateCommunityRequest ArchiveCommunityBody() => new(Archive: true);

  private static CommunityAutomodSimulationRequest CommunityAutomodSimulationFixtureBody() =>
      new("prompt-1");

  private static CreateEntityRelationBody CreateEntityRelationFixtureBody() =>
      new("topic-2");

  private static ImportCommunityListBody ImportCommunityListFixtureBody() =>
      new("test-community");

  private static UpdateLandingPageBody LandingPageUpdateBody() =>
      new(Title: "Updated Links", Slug: "updated-links");
}
