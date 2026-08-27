import VouchaAPI
import VouchaModels

extension EndpointManifestCoverage {
    static let communityEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "web.communities.search.default") {
            Endpoint.communities(query: "test")
        },
        ManifestRegisteredEndpoint(id: "web.communities.show.default") {
            Endpoint.community(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.ai-agent.default") {
            Endpoint.enableCommunityAiAgent(idOrSlug: "test-community", agentSlug: "self-promotion")
        },
        ManifestRegisteredEndpoint(id: "web.communities.archive.default") {
            Endpoint.updateCommunity(idOrSlug: "test-community", archive: true)
        },
        ManifestRegisteredEndpoint(id: "web.communities.members.default") {
            Endpoint.communityMembers(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.posts.default") {
            Endpoint.communityPosts(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.news.default") {
            Endpoint.communityNews(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.topics.default") {
            Endpoint.communityListItems(idOrSlug: "test-community", itemType: .topic)
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.rss-feeds.default") {
            Endpoint.communityListItems(idOrSlug: "test-community", itemType: .rssFeed)
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.posts.default") {
            Endpoint.communityListItems(idOrSlug: "test-community", itemType: .post)
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.domains.default") {
            Endpoint.communityListItems(idOrSlug: "test-community", itemType: .urlHostname)
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.urls.default") {
            Endpoint.communityListItems(idOrSlug: "test-community", itemType: .url)
        },
        ManifestRegisteredEndpoint(id: "web.communities.list-items.counts.default") {
            Endpoint.communityListItemCounts(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.application-questions.default") {
            Endpoint.communityApplicationQuestions(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.pinned-posts.default") {
            Endpoint.communityPinnedPosts(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.saved-replies.default") {
            Endpoint.communitySavedReplies(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.saved-replies.page-2") {
            Endpoint.communitySavedReplies(
                idOrSlug: "test-community",
                after: "eyJyYW5raW5nIjowLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDcwMSJ9",
                limit: 25
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.saved-reply.create.default") {
            Endpoint.createCommunitySavedReply(
                idOrSlug: "test-community",
                title: "Greeting",
                body: "Thanks for writing in."
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.applications.default") {
            Endpoint.communityApplications(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.invites.default") {
            Endpoint.communityInvites(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.bans.default") {
            Endpoint.communityBans(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.restrictions.default") {
            Endpoint.communityRestrictions(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.modmail.default") {
            Endpoint.communityModmail(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.modmail.page-2") {
            Endpoint.communityModmail(
                idOrSlug: "test-community",
                after: "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDEyOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDUwMSJ9",
                limit: 25
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.modmail-messages.default") {
            Endpoint.communityModmailMessages(
                idOrSlug: "test-community",
                conversationId: "00000000-0000-7000-8000-000000000501"
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.modmail-messages.page-2") {
            Endpoint.communityModmailMessages(
                idOrSlug: "test-community",
                conversationId: "00000000-0000-7000-8000-000000000501",
                after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDYwMSJ9",
                limit: 25
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.modlog.default") {
            Endpoint.communityModlog(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.moderation-queue.default") {
            Endpoint.communityModerationQueue(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.moderation-analytics.default") {
            Endpoint.communityModerationAnalytics(idOrSlug: "test-community", range: "30d")
        },
        ManifestRegisteredEndpoint(id: "web.communities.moderator-stats.default") {
            Endpoint.communityModeratorStats(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.moderator-vacation.default") {
            Endpoint.communityModeratorVacation(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.ai-agents.default") {
            Endpoint.communityAiAgents(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.agent-prompts.default") {
            Endpoint.communityAgentPrompts(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.agent-prompts.history.default") {
            Endpoint.communityAgentPromptHistory(idOrSlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "web.communities.automod-simulate.default") {
            Endpoint.simulateCommunityAutomod(
                idOrSlug: "test-community",
                body: CommunityAutomodSimulationBody(promptId: "prompt-1")
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.automod-recent-actions.default") {
            Endpoint.communityAutomodRecentActions(
                idOrSlug: "test-community",
                limit: 25,
                window: "24h",
                source: "agent_moderation"
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.automod-feedback.create.default") {
            Endpoint.recordCommunityAutomodFeedback(
                idOrSlug: "test-community",
                sourceKey: "agent_moderation:post-1",
                outcome: .truePositive,
                action: .keepRemoved,
                reasonCode: "correct",
                note: "Matches the community rules."
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.moderation-results.default") {
            Endpoint.communityModerationResults(idOrSlug: "test-community", postId: "post-1")
        },
        ManifestRegisteredEndpoint(id: "web.communities.warning.create.default") {
            Endpoint.createCommunityWarning(
                idOrSlug: "test-community",
                userId: "user-1",
                reason: "Spam in community",
                publicMessage: "Please read the community rules.",
                resolveReport: false
            )
        },
        ManifestRegisteredEndpoint(id: "web.communities.post-type-settings.update.default") {
            Endpoint.updateCommunityPostTypeSettings(
                idOrSlug: "test-community",
                allowReviewPosts: true,
                allowDataPointPosts: true
            )
        },
        ManifestRegisteredEndpoint(id: "web.topics.search.default") {
            Endpoint.topics(query: "tech")
        },
        ManifestRegisteredEndpoint(id: "web.topics.mutation.default") {
            Endpoint.createTopic(body: CreateTopicBody(name: "Tech", slug: "tech", topicType: "topic"))
        },
        ManifestRegisteredEndpoint(id: "web.rss-feed-items.feed.default") {
            Endpoint.rssFeedItems(feedType: "any", limit: 25, mediaType: "article")
        },
        ManifestRegisteredEndpoint(id: "web.topics.publisher-types.default") {
            Endpoint.publisherTypes()
        },
        ManifestRegisteredEndpoint(id: "native.topics.user-tags.default") {
            Endpoint.userTags()
        }
    ]
}
