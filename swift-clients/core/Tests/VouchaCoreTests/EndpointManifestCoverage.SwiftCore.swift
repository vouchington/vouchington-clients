import VouchaAPI

extension EndpointManifestCoverage {
    static let swiftCoreEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "swift.posts.feed.default") {
            Endpoint.posts(feedType: "any")
        },
        ManifestRegisteredEndpoint(id: "swift.notifications.default") {
            Endpoint.notifications()
        },
        ManifestRegisteredEndpoint(id: "native.notifications.redirect-target.default") {
            Endpoint.notificationRedirectTarget(notificationId: "notification-1")
        },
        ManifestRegisteredEndpoint(id: "swift.users.following.default") {
            Endpoint.userFollowing(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "swift.users.followers.default") {
            Endpoint.userFollowers(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "swift.my.identity.default") {
            Endpoint.myIdentity
        },
        ManifestRegisteredEndpoint(id: "native.my.email-preferences.default") {
            Endpoint.myEmailPreferences
        },
        ManifestRegisteredEndpoint(id: "native.my.email-addresses.empty") {
            Endpoint.myEmailAddresses(after: "fixture-owner-scoped-email-cursor", limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.my.email-addresses.request.default") {
            Endpoint.requestMyEmailAddressVerification(emailAddress: " Tests+Native-User@Voucha.ai ")
        },
        ManifestRegisteredEndpoint(id: "native.my.email-addresses.verify.default") {
            Endpoint.verifyMyEmailAddress(emailAddress: "tests+native-user@voucha.ai", token: "ABCD1234")
        },
        ManifestRegisteredEndpoint(id: "native.users.delete.default") {
            Endpoint.deleteUser(idOrSlug: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.users.data-request.default") {
            Endpoint.userDataRequest(idOrSlug: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.users.data-request.create.default") {
            Endpoint.createUserDataRequest(idOrSlug: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.auth.sessions.default") {
            Endpoint.authSessions(after: "fixture-owner-scoped-session-cursor", limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.my.api-keys.paginated") {
            Endpoint.myApiKeys(after: "fixture-owner-scoped-api-key-cursor", limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.my.push-subscriptions.paginated") {
            Endpoint.myPushSubscriptions(after: "fixture-owner-scoped-push-cursor", limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.community.pending-reports.paginated") {
            Endpoint.communityPendingReports(
                idOrSlug: "fixture-community",
                after: "fixture-community-role-and-sort-scoped-report-cursor",
                limit: 1
            )
        },
        ManifestRegisteredEndpoint(id: "native.auth.bluesky.link.default") {
            Endpoint.beginBlueskyAccountLink(handle: "alice.bsky.social")
        },
        ManifestRegisteredEndpoint(id: "native.auth.bluesky.link.native") {
            Endpoint.beginNativeBlueskyAccountLink(
                handle: "alice.bsky.social",
                completionProofChallenge: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
            )
        },
        ManifestRegisteredEndpoint(id: "native.auth.bluesky.link-completion.default") {
            Endpoint.completeNativeBlueskyAccountLink(
                flowId: "00000000-0000-7000-8000-00000000b501",
                completionToken: "fixture-completion-token",
                completionProofVerifier: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
            )
        },
        ManifestRegisteredEndpoint(id: "native.auth.bluesky.unlink.default") {
            Endpoint.disconnectBlueskyAccount
        },
        ManifestRegisteredEndpoint(id: "native.fediverse.instances.default") {
            Endpoint.fediverseInstances()
        },
        ManifestRegisteredEndpoint(id: "native.fediverse.instances.page-2") {
            Endpoint.fediverseInstances(after: "next", sort: nil)
        },
        ManifestRegisteredEndpoint(id: "native.fediverse.instances.empty") {
            Endpoint.fediverseInstances(limit: nil, query: "no-match-fixture-query", sort: nil)
        },
        ManifestRegisteredEndpoint(id: "native.fediverse.instance.slug") {
            Endpoint.fediverseInstance(idOrSlug: "social-example")
        },
        ManifestRegisteredEndpoint(id: "native.fediverse.instance.uuid") {
            Endpoint.fediverseInstance(idOrSlug: "00000000-0000-7000-8000-00000000f003")
        },
        ManifestRegisteredEndpoint(id: "swift.my.profile.default") {
            Endpoint.myProfile
        },
        ManifestRegisteredEndpoint(id: "swift.rss-feeds.default") {
            Endpoint.allRssFeeds(limit: 25)
        },
        ManifestRegisteredEndpoint(id: "swift.rss-feed-items.feed.default") {
            Endpoint.rssFeedItems(feedType: "any", mediaType: "video")
        },
        ManifestRegisteredEndpoint(id: "native.rss-feed-item.detail.default") {
            Endpoint.rssFeedItem(id: "00000000-0000-7000-8000-000000007886")
        },
        ManifestRegisteredEndpoint(id: "native.topic-recommendation.detail.default") {
            Endpoint.topicRecommendation(id: "recommendation-1")
        },
        ManifestRegisteredEndpoint(id: "native.topic-recommendations.top-hashtags.default") {
            Endpoint.topHashtags()
        },
        ManifestRegisteredEndpoint(id: "swift.integration.rss-feed-items.video") {
            Endpoint.rssFeedItems(feedType: "any", mediaType: "video")
        },
        ManifestRegisteredEndpoint(id: "swift.integration.rss-feed-items.audio") {
            Endpoint.rssFeedItems(feedType: "any", mediaType: "audio")
        },
        ManifestRegisteredEndpoint(id: "swift.podcast-playback-position.default") {
            Endpoint.podcastPlaybackPosition(rssFeedItemId: "episode-1")
        },
        ManifestRegisteredEndpoint(id: "swift.podcast-episode-chapters.default") {
            Endpoint.podcastEpisodeChapters(rssFeedItemId: "episode-1")
        },
        ManifestRegisteredEndpoint(id: "web.growth-metrics.default") {
            Endpoint.growthMetrics()
        },
        ManifestRegisteredEndpoint(id: "shared.currencies.list.default") {
            Endpoint.currencies()
        }
    ]
}
