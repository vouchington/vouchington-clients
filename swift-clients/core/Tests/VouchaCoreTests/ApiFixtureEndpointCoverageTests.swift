@testable import VouchaAPI
import VouchaModels
import XCTest

final class ApiFixtureEndpointCoverageTests: XCTestCase {
    private static let messageConversationId = "00000000-0000-7000-8000-000000000101"
    private static let messageParticipantUserId = "00000000-0000-7000-8000-000000000002"
    private static let messageAddedUserId = "00000000-0000-7000-8000-000000000003"
    private static let importExportBatchId = "70000000-0000-7000-8000-000000000001"
    private static let moderationFixtureEndpoints: [String: Endpoint] = Dictionary(
        uniqueKeysWithValues:
        Array(moderationEndpointRegistry) + Array(moderationParityEndpointRegistry)
    )

    static let registry: [String: Endpoint] = moderationFixtureEndpoints
        .merging(entityProvenanceFixtureEndpoints) { _, replacement in replacement }
        .merging(userProfileFixtureEndpointRegistry) { _, replacement in replacement }
        .merging(credentialFixtureEndpoints) { _, replacement in replacement }
        .merging(currentContractFixtureEndpoints) { _, replacement in replacement }
        .merging([
            "native.import-export.rss-feeds.submit.default": .importRssFeeds(
                .urls(["https://example.test/feed.xml", "https://invalid.example.test/feed.xml"])
            ),
            "native.identity-verification-attempts.grant.default": .grantIdentityVerificationAttempt(
                userId: "00000000-0000-7000-8000-000000000003",
                note: "Provider terminal error reviewed by support."
            ),
            "native.import-export.rss-feeds.status.retrying": .rssFeedImportStatus(importId: importExportBatchId),
            "native.import-export.rss-feeds.status.partial": .rssFeedImportStatus(importId: importExportBatchId),
            "native.import-export.topics.import.outcomes": .importTopics(["Travel", "Local News", "Travel", "!!!"]),
            "native.import-export.topics.export.default": .exportTopics,
            "native.import-export.topics.export.download": .exportTopicsDownload,
            "web.communities.archive.default": Endpoint.updateCommunity(
                idOrSlug: "test-community",
                archive: true
            ),
            "web.communities.members.default": Endpoint.communityMembers(idOrSlug: "test-community"),
            "web.communities.posts.default": Endpoint.communityPosts(idOrSlug: "test-community"),
            "web.communities.news.default": Endpoint.communityNews(idOrSlug: "test-community"),
            "web.communities.list-items.topics.default": Endpoint.communityListItems(
                idOrSlug: "test-community",
                itemType: .topic
            ),
            "web.communities.list-items.rss-feeds.default": Endpoint.communityListItems(
                idOrSlug: "test-community",
                itemType: .rssFeed
            ),
            "web.communities.list-items.posts.default": Endpoint.communityListItems(
                idOrSlug: "test-community",
                itemType: .post
            ),
            "web.communities.list-items.domains.default": Endpoint.communityListItems(
                idOrSlug: "test-community",
                itemType: .urlHostname
            ),
            "web.communities.list-items.urls.default": Endpoint.communityListItems(
                idOrSlug: "test-community",
                itemType: .url
            ),
            "web.communities.list-items.counts.default": Endpoint.communityListItemCounts(
                idOrSlug: "test-community"
            ),
            "web.communities.application-questions.default": Endpoint.communityApplicationQuestions(
                idOrSlug: "test-community"
            ),
            "web.communities.pinned-posts.default": Endpoint.communityPinnedPosts(idOrSlug: "test-community"),
            "web.communities.saved-replies.default": Endpoint.communitySavedReplies(idOrSlug: "test-community"),
            "web.communities.saved-replies.page-2": Endpoint.communitySavedReplies(
                idOrSlug: "test-community",
                after: "eyJyYW5raW5nIjowLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcwMSJ9",
                limit: 25
            ),
            "web.communities.saved-reply.create.default": Endpoint.createCommunitySavedReply(
                idOrSlug: "test-community",
                title: "Greeting",
                body: "Thanks for writing in."
            ),
            "web.communities.applications.default": Endpoint.communityApplications(idOrSlug: "test-community"),
            "web.communities.invites.default": Endpoint.communityInvites(idOrSlug: "test-community"),
            "web.communities.bans.default": Endpoint.communityBans(idOrSlug: "test-community"),
            "web.communities.restrictions.default": Endpoint.communityRestrictions(idOrSlug: "test-community"),
            "web.communities.modmail.default": Endpoint.communityModmail(idOrSlug: "test-community"),
            "web.communities.modmail.page-2": Endpoint.communityModmail(
                idOrSlug: "test-community",
                after: "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDEyOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDUwMSJ9",
                limit: 25
            ),
            "web.communities.modmail-messages.default": Endpoint.communityModmailMessages(
                idOrSlug: "test-community",
                conversationId: "00000000-0000-7000-8000-000000000501"
            ),
            "web.communities.modmail-messages.page-2": Endpoint.communityModmailMessages(
                idOrSlug: "test-community",
                conversationId: "00000000-0000-7000-8000-000000000501",
                after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDYwMSJ9",
                limit: 25
            ),
            "web.communities.modlog.default": Endpoint.communityModlog(idOrSlug: "test-community"),
            "web.communities.moderation-queue.default": Endpoint.communityModerationQueue(
                idOrSlug: "test-community"
            ),
            "web.communities.moderation-analytics.default": Endpoint.communityModerationAnalytics(
                idOrSlug: "test-community",
                range: "30d"
            ),
            "web.communities.moderator-stats.default": Endpoint.communityModeratorStats(idOrSlug: "test-community"),
            "web.communities.moderator-vacation.default": Endpoint.communityModeratorVacation(
                idOrSlug: "test-community"
            ),
            "web.communities.ai-agents.default": Endpoint.communityAiAgents(idOrSlug: "test-community"),
            "web.communities.agent-prompts.default": Endpoint.communityAgentPrompts(idOrSlug: "test-community"),
            "web.communities.agent-prompts.history.default": Endpoint.communityAgentPromptHistory(
                idOrSlug: "test-community"
            ),
            "web.communities.automod-simulate.default": Endpoint.simulateCommunityAutomod(
                idOrSlug: "test-community",
                body: CommunityAutomodSimulationBody(promptId: "prompt-1")
            ),
            "web.communities.automod-recent-actions.default": Endpoint.communityAutomodRecentActions(
                idOrSlug: "test-community",
                limit: 25,
                window: "24h",
                source: "agent_moderation"
            ),
            "web.communities.automod-feedback.create.default": Endpoint.recordCommunityAutomodFeedback(
                idOrSlug: "test-community",
                sourceKey: "agent_moderation:post-1",
                outcome: .truePositive,
                action: .keepRemoved,
                reasonCode: "correct",
                note: "Matches the community rules."
            ),
            "web.communities.moderation-results.default": Endpoint.communityModerationResults(
                idOrSlug: "test-community",
                postId: "post-1"
            ),
            "web.communities.warning.create.default": Endpoint.createCommunityWarning(
                idOrSlug: "test-community",
                userId: "user-1",
                reason: "Spam in community",
                publicMessage: "Please read the community rules.",
                resolveReport: false
            ),
            "web.communities.post-type-settings.update.default": Endpoint.updateCommunityPostTypeSettings(
                idOrSlug: "test-community",
                shouldAllowReviewPosts: true,
                shouldAllowDataPointPosts: true
            ),
            "web.growth-metrics.default": Endpoint.growthMetrics(),
            "native.admin-ai-costs.default": Endpoint.adminAiCosts(),
            "native.admin-ai-costs.page-2": Endpoint.adminAiCosts(
                after: "eyJ0b3RhbF9jb3N0X21pY3JvdW5pdHMiOiI5MDA3MTk5MjU0NzQwOTkzMTIzNDU2NzgiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiJ9"
            ),
            "native.admin-ai-costs.empty": Endpoint.adminAiCosts(),
            "shared.currencies.list.default": Endpoint.currencies(),
            "web.topics.publisher-types.default": Endpoint.publisherTypes(),
            "native.topics.user-tags.default": Endpoint.userTags(),
            "web.topics.search.referral-programs.default": Endpoint.topics(
                query: "test",
                topicTypes: ["referral_program"],
                limit: 10
            ),
            "web.referral-links.feed.default": Endpoint.referralLinksFeed(feedType: "follow_users"),
            "native.referral-links.mine.default": Endpoint.referralLinks(),
            "native.referral-clicks.mine.default": Endpoint.myReferralClicks(),
            "web.trending-referral-programs.default": Endpoint.trendingReferralPrograms(),
            "web.referral-links.prioritized.default": Endpoint.prioritizedReferralLinks(
                referralProgramId: "referral-program-1",
                all: true
            ),
            "web.admin.article-syncs.trigger.default": Endpoint.articleSyncTrigger,
            "web.admin.article-syncs.status.active": Endpoint.articleSyncStatus(jobId: "job-1"),
            "web.admin.mq.stats.default": Endpoint.mqStats,
            "web.admin.mq.queues.default": Endpoint.mqQueues,
            "web.admin.mq.queues.pause.default": Endpoint.mqPauseQueue(name: "psql"),
            "web.admin.mq.queues.resume.default": Endpoint.mqResumeQueue(name: "psql"),
            "web.admin.mq.scheduled-jobs.default": Endpoint.mqScheduledJobs,
            "web.admin.mq.scheduled-jobs.trigger.default": Endpoint.mqRunScheduledJob(
                id: "kagi-smallweb-sync"
            ),
            "web.admin.mq.backfills.default": Endpoint.mqBackfills,
            "web.admin.mq.backfills.trigger.default": Endpoint.mqRunBackfill(
                id: "openai-moderation-posts"
            ),
            "web.admin.psql.migrations.default": Endpoint.psqlMigrations,
            "web.admin.psql.partitions.default": Endpoint.psqlPartitions,
            "web.admin.psql.jobs.default": Endpoint.psqlJobs(type: .runConfigDriven),
            "web.admin.valkey.bloom-filters.rebuild.default": Endpoint.valkeyRebuildBloomFilter(
                filter: .entityCache
            ),
            "web.admin.valkey.cache-groups.default": Endpoint.valkeyCacheGroups,
            "web.admin.valkey.caches.clear.default": Endpoint.valkeyClearCache(group: "posts"),
            "web.admin.valkey.flush.default": Endpoint.valkeyFlush(concern: .blooms),
            "native.entity-relations.post.category.topic.default": Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 1,
                after: "fixture-relation-scope-and-sort-cursor"
            ),
            "native.entity-relations.post.related.post.default": Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "related",
                objectType: "post",
                sort: "best",
                limit: 100
            ),
            "native.entity-relations.post.related.url.default": Endpoint.entityRelations(
                entityType: "post",
                entityId: "post-1",
                predicate: "related",
                objectType: "url",
                sort: "best",
                limit: 100
            ),
            "native.entity-relations.rss-feed-item.category.topic.default": Endpoint.entityRelations(
                entityType: "rss_feed_item",
                entityId: "item-1",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 100
            ),
            "native.entity-relations.topic.publisher-type.topic.default": Endpoint.entityRelations(
                entityType: "topic",
                entityId: "topic-1",
                predicate: "publisher_type",
                objectType: "topic",
                sort: "best",
                limit: 100
            ),
            "native.entity-relations.post.category.topic.create.default": Endpoint.createEntityRelation(
                entityType: "post",
                entityId: "post-1",
                predicate: "category",
                objectType: "topic",
                objectId: "topic-2"
            ),
            "native.entity-relations.user.category.topic.default": Endpoint.entityRelations(
                entityType: "user",
                entityId: "user-abc",
                predicate: "category",
                objectType: "topic",
                sort: "best",
                limit: 100,
                positiveNetVoteScore: true
            ),
            "native.entity-relations.user.category.topic.create.default": Endpoint.createEntityRelation(
                entityType: "user",
                entityId: "user-abc",
                predicate: "category",
                objectType: "topic",
                objectId: "user-tag-bot"
            ),
            "native.entity-relations.post.category.topic.vote.default": Endpoint.voteEntityRelation(
                relationId: "relation-post-category-topic-1",
                choice: .confirm
            ),
            "native.rss-feed-item.detail.default": Endpoint.rssFeedItem(
                id: "00000000-0000-7000-8000-000000007886"
            ),
            "native.topic-recommendation.detail.default": Endpoint.topicRecommendation(id: "recommendation-1"),
            "native.topic-recommendations.top-hashtags.default": Endpoint.topHashtags(),
            "native.bookmarks.posts.saved.default": Endpoint.userPosts(userId: "user-abc", listType: "saved"),
            "native.bookmarks.posts.saved.next-page": Endpoint.userPosts(
                userId: "user-abc",
                listType: "saved",
                after: "eyJ0aW1lc3RhbXAiOjE3NjcyMjU2MDAwMDAwMDAsImlkIjoiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMDAxIiwic2NvcGUiOiJ1c2VyLXBvc3RzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDA5OTpzYXZlZDpjcmVhdGVkLWF0LWRlc2Mtb2JqZWN0LWlkLWRlc2MifQ"
            ),
            "native.bookmarks.topics.muted.default": Endpoint.userTopics(userId: "user-abc", listType: "muted"),
            "native.bookmarks.topics.viewed.default": Endpoint.userTopics(userId: "user-abc", listType: "viewed"),
            "native.bookmarks.users.subscribed-posts.default": Endpoint.userUsers(
                userId: "user-abc",
                listType: "subscribed-posts"
            ),
            "native.bookmarks.users.dismissed-recommendations.default": Endpoint.userUsers(
                userId: "user-abc",
                listType: "dismissed-recommendations"
            ),
            "native.bookmarks.rss-feed-items.saved.default": Endpoint.userRssFeedItems(
                userId: "user-abc",
                listType: "saved",
                mediaType: "article"
            ),
            "native.bookmarks.rss-feeds.muted.default": Endpoint.userRssFeeds(
                userId: "user-abc",
                listType: "muted",
                feedType: "article",
                limit: 25
            ),
            "native.bookmarks.rss-feeds.viewed.default": Endpoint.userRssFeeds(
                userId: "user-abc",
                listType: "viewed",
                feedType: "podcast",
                limit: 25
            ),
            "native.bookmarks.urls.saved.default": Endpoint.userUrls(userId: "user-abc", listType: "saved"),
            "native.bookmarks.domains.blocked.default": Endpoint.userHostnames(
                userId: "user-abc",
                listType: "blocked"
            ),
            "native.bookmarks.domains.muted.default": Endpoint.userHostnames(userId: "user-abc", listType: "muted"),
            "native.bookmarks.communities.proxy-following.default": Endpoint.userCommunities(
                userId: "user-abc",
                listType: "proxy-following"
            ),
            "swift.posts.feed.default": Endpoint.posts(feedType: "any"),
            "swift.notifications.default": Endpoint.notifications(),
            "native.notifications.redirect-target.default": Endpoint
                .notificationRedirectTarget(notificationId: "notification-1"),
            "swift.users.following.default": Endpoint.userFollowing(userId: "user-abc"),
            "swift.users.followers.default": Endpoint.userFollowers(userId: "user-abc"),
            "swift.rss-feeds.default": Endpoint.allRssFeeds(limit: 25),
            "swift.rss-feed-items.feed.default": Endpoint.rssFeedItems(mediaType: "video"),
            "swift.integration.rss-feed-items.video": Endpoint.rssFeedItems(mediaType: "video"),
            "swift.integration.rss-feed-items.audio": Endpoint.rssFeedItems(mediaType: "audio"),
            "native.comments.post-detail.default": Endpoint.post(idOrSlug: "comment-root"),
            "native.comments.descendants.default": Endpoint.postDescendants(
                postId: "comment-root-post",
                after: "fixture-root-and-subtree-scoped-cursor",
                limit: 2
            ),
            "native.comments.ancestors.permalink": Endpoint.postAncestors(postId: "comment-b"),
            "native.lists.default": Endpoint.lists(),
            "native.list-items.default": Endpoint.listItems(listId: "list-1"),
            "native.hostname.default": Endpoint.hostname(idOrHostname: "hostname-1"),
            "native.hostnames.default": Endpoint.hostnames(query: "example"),
            "native.urls.default": Endpoint.urls(query: "example"),
            "native.url.default": Endpoint.url(urlId: "url-1"),
            "native.url-crawls.default": Endpoint.urlCrawls(urlId: "url-1"),
            "native.paid.url-crawls.default": Endpoint.urlCrawls(urlId: "url-1"),
            "native.url-crawl.default": Endpoint.urlCrawl(urlId: "url-1", crawlId: "crawl-1"),
            "native.paid.url-crawl.default": Endpoint.urlCrawl(urlId: "url-1", crawlId: "crawl-1"),
            "web.paid.rss-feed-crawls.default": Endpoint.rssFeedCrawls(id: "rss-feed-1"),
            "web.paid.rss-feed-crawl.default": Endpoint.rssFeedCrawl(id: "rss-feed-1", crawlId: "crawl-1"),
            "native.url-crawl-trigger.default": Endpoint.triggerUrlCrawl(urlId: "url-1"),
            "native.landing-pages.default": Endpoint.myLandingPages,
            "native.landing-page-detail.default": Endpoint.myLandingPage(id: "landing-page-1"),
            "native.landing-page-analytics.default": Endpoint.myLandingPageAnalytics(pageId: "landing-page-1"),
            "native.admin-user-landing-pages.default": Endpoint.adminUserLandingPages(userId: "user-abc"),
            "native.admin-landing-page-analytics.default": Endpoint.adminLandingPageAnalytics(pageId: "landing-page-1"),
            "native.landing-page-candidates.default": Endpoint.myLandingPageCandidates,
            "native.landing-page-items-mutation.default": nativeLandingPageItemsMutationEndpoint(),
            "native.landing-page-mutation.default": Endpoint.updateMyLandingPage(
                id: "landing-page-1",
                body: LandingPageMetadataBody(title: "Updated Links", slug: "updated-links")
            ),
            "native.list-import.default": Endpoint.importCommunityList(
                listId: "list-1",
                communitySlug: "test-community"
            ),
            "native.lists-containing.default": Endpoint.listsContaining(
                itemType: "rss_feed_item",
                entityId: "item-1"
            ),
            "native.messages.conversations.default": Endpoint.myMessages(),
            "native.messages.conversations.page-2": Endpoint.myMessages(
                after: "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDA5OjE1OjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiJ9"
            ),
            "native.messages.conversation.default": Endpoint.myMessageConversation(
                conversationId: messageConversationId
            ),
            "native.messages.create.default": Endpoint.createMyMessageConversation(
                userIds: [messageParticipantUserId]
            ),
            "native.messages.thread.default": Endpoint.myMessageConversationMessages(
                conversationId: messageConversationId
            ),
            "native.messages.thread.page-2": Endpoint.myMessageConversationMessages(
                conversationId: messageConversationId,
                after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSJ9",
                limit: 50
            ),
            "native.messages.send.default": Endpoint.sendMyMessageConversationMessage(
                conversationId: messageConversationId,
                text: "Sent from native"
            ),
            "native.messages.participants.default": Endpoint.myMessageConversationParticipants(
                conversationId: messageConversationId
            ),
            "native.messages.participant-add.default": Endpoint.addMyMessageConversationParticipant(
                conversationId: messageConversationId,
                userId: messageAddedUserId
            ),
            "native.messages.participant-remove.default": Endpoint.removeMyMessageConversationParticipant(
                conversationId: messageConversationId,
                userId: messageParticipantUserId
            ),
            "native.messages.policy.default": Endpoint.updateMyMessageConversationParticipantAddPolicy(
                conversationId: messageConversationId,
                policy: .ownerOnly
            ),
            "native.messages.user-search.default": Endpoint.myMessageUserSearch(query: "bo", limit: 10),
            "native.memberships.plans.default": Endpoint.membershipPlans,
            "native.memberships.grant.default": Endpoint.grantMembership(
                userId: "00000000-0000-7000-8000-000000000003",
                plan: .plus,
                skuId: "00000000-0000-7000-8000-000000000701",
                durationDays: 30
            ),
            "swift.podcast-episode-chapters.default": Endpoint.podcastEpisodeChapters(
                rssFeedItemId: "episode-1"
            ),
            "swift.podcast-playback-position.default": Endpoint.podcastPlaybackPosition(
                rssFeedItemId: "episode-1"
            )
        ], uniquingKeysWith: { _, endpoint in endpoint })
        .merging(accountPaginationFixtureEndpointRegistry) { _, replacement in replacement }
        .merging(moderationIntegrityEndpointFixtureCoverage) { _, replacement in replacement }
        .merging(householdFixtureEndpoints) { _, replacement in replacement }
        .merging(paymentCardFixtureEndpoints) { _, replacement in replacement }
        .merging(pointValuationFixtureEndpoints) { _, replacement in replacement }
        .merging(spendingCategoryFixtureEndpoints) { _, replacement in replacement }
        .merging(rewardsProgramStatusFixtureEndpoints) { _, replacement in replacement }
        .merging(dynamicConfigEndpointFixtureCoverage) { _, replacement in replacement }
        .merging(moderationAppealEndpointRegistry) { _, replacement in replacement }
        .merging(nativeOAuthAndFriendRecommendationFixtureEndpoints) { _, replacement in replacement }
        .merging(followerDistributionFixtureEndpoints) { _, replacement in replacement }
        .merging(copyrightMediaFixtureEndpoints) { _, replacement in replacement }
        .merging(storyFixtureEndpoints) { _, replacement in replacement }
        .merging(membershipGrantAndAncestorFixtureEndpoints) { _, replacement in replacement }
        .merging(membershipStoreFixtureEndpoints) { _, replacement in replacement }
}
