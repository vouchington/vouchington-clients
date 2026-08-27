import VouchaAPI
import VouchaModels

private struct NotificationRedirectTargetCoverageResponse: Codable {
    let targetUrl: String
}

let accountFeedFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "swift.posts.feed.default") {
        try assertFixtureCoversDTO(
            $0,
            as: PostFeedResponse.self,
            ignoring: [
                "communities",
                "communities.community-1",
                "communities.community-1.__entity_type",
                "communities.community-1.allow_data_point_posts",
                "communities.community-1.allow_review_posts",
                "communities.community-1.archived_at",
                "communities.community-1.archived_by_id",
                "communities.community-1.banner_image_id",
                "communities.community-1.created_at",
                "communities.community-1.created_by_id",
                "communities.community-1.default_language",
                "communities.community-1.deleted_at",
                "communities.community-1.deleted_by_id",
                "communities.community-1.id",
                "communities.community-1.lingua_rs_detected_language",
                "communities.community-1.list_type",
                "communities.community-1.markdown",
                "communities.community-1.member_invites_allowed_at",
                "communities.community-1.member_roster_visibility",
                "communities.community-1.name",
                "communities.community-1.post_approval_required_at",
                "communities.community-1.profile_image_id",
                "communities.community-1.rules_markdown",
                "communities.community-1.slug",
                "communities.community-1.trusted_at",
                "communities.community-1.updated_at",
                "communities.community-1.visibility",
                "election_votes.p1.__entity_type",
                "election_votes.p1.created_at",
                "election_votes.p1.entity_id",
                "election_votes.p1.user_id",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "post_elections.p1.__entity_type",
                "post_elections.p1.id",
                "posts.p1.broadcast",
                "posts.p1.clearance_status",
                "posts.p1.html",
                "posts.p1.markdown",
                "posts.p1.parent_id",
                "posts.p1.root_id",
                "users",
                "users.user-1",
                "users.user-1.__entity_type",
                "users.user-1.created_at",
                "users.user-1.id",
                "users.user-1.name",
                "users.user-1.profile_image_id",
                "users.user-1.updated_at",
                "users.user-1.username"
            ]
        )
    },
    RegisteredFixture(id: "swift.notifications.default") {
        try assertFixtureCoversDTO(
            $0,
            as: NotificationsResponse.self,
            ignoring: [
                "notifications.00000000-0000-7000-8000-000000000101.read_at",
                "notifications.00000000-0000-7000-8000-000000000101.rss_feed_item_id",
                "notifications.00000000-0000-7000-8000-000000000101.target_path",
                "notifications.00000000-0000-7000-8000-000000000102.read_at",
                "notifications.00000000-0000-7000-8000-000000000102.rss_feed_item_id",
                "notifications.00000000-0000-7000-8000-000000000102.target_path",
                "notifications.n1.read_at",
                "notifications.n1.rss_feed_item_id",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "posts.p1.broadcast",
                "posts.p1.clearance_status",
                "posts.p1.html",
                "posts.p1.markdown",
                "posts.p1.parent_id",
                "posts.p1.root_id",
                "results.__entity_type",
                "users.user-1.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "native.notifications.redirect-target.default") {
        try assertFixtureCoversDTO($0, as: NotificationRedirectTargetCoverageResponse.self)
    },
    RegisteredFixture(id: "swift.users.following.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: [
                "muted",
                "muted.friend-1",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.markdown",
                "results.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "swift.users.followers.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: [
                "muted",
                "muted.friend-1",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.markdown",
                "results.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "native.users.followers.search") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: [
                "muted",
                "muted.friend-1",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.markdown",
                "results.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "native.posts.followers.share") {
        try assertFixtureCoversDTO($0, as: FollowerDistributionAcceptedResponse.self)
    },
    RegisteredFixture(id: "native.posts.followers.send-selected") {
        try assertFixtureCoversDTO($0, as: FollowerDistributionAcceptedResponse.self)
    },
    RegisteredFixture(id: "native.rss-feed-items.followers.share") {
        try assertFixtureCoversDTO($0, as: FollowerDistributionAcceptedResponse.self)
    },
    RegisteredFixture(id: "native.rss-feed-items.followers.send-selected") {
        try assertFixtureCoversDTO($0, as: FollowerDistributionAcceptedResponse.self)
    },
    RegisteredFixture(id: "swift.podcast-playback-position.default") {
        try assertFixtureCoversDTO(
            $0,
            as: PodcastPlaybackPositionResponse.self,
            ignoring: ["playback_position.completed_at"]
        )
    },
    RegisteredFixture(id: "swift.my.profile.default") {
        try assertFixtureCoversDTO($0, as: ProfileCoverageEnvelope.self)
    },
    RegisteredFixture(id: "swift.my.identity.default") {
        try assertFixtureCoversDTO(
            $0,
            as: IdentityCoverageEnvelope.self,
            ignoring: [
                "identity.markdown",
                "identity.profile_image_id",
                "identity.suspended_at",
                "identity.verification_status"
            ]
        )
    },
    RegisteredFixture(id: "native.my.email-preferences.default") {
        try assertFixtureCoversDTO($0, as: EmailPreferencesResponse.self)
    },
    RegisteredFixture(id: "native.users.delete.default") {
        try assertFixtureCoversDTO($0, as: DecodedJSONValue.self)
    },
    RegisteredFixture(id: "native.users.data-request.default") {
        try assertFixtureCoversDTO($0, as: UserDataRequest.self)
    },
    RegisteredFixture(id: "native.users.data-request.create.default") {
        try assertFixtureCoversDTO($0, as: UserDataRequest.self)
    },
    RegisteredFixture(id: "native.auth.sessions.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<AuthSession>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.auth.bluesky.link.default") {
        try assertFixtureCoversDTO($0, as: BeginBlueskyAccountLinkResponse.self)
    },
    RegisteredFixture(id: "swift.rss-feeds.default") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedSourceListResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.podcast_show"
            ]
        )
    },
    RegisteredFixture(id: "swift.rss-feed-items.feed.default") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemFeedResponse.self,
            ignoring: [
                "election_votes.item-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.story_id",
                "rss_feed_item_elections.item-1.__entity_type",
                "rss_feed_item_elections.item-1.id",
                "rss_feed_items.item-1.content",
                "rss_feed_items.item-1.creator",
                "rss_feed_items.item-1.media_content.duration",
                "rss_feed_items.item-peer.content",
                "rss_feed_items.item-peer.creator",
                "rss_feed_items.item-peer.media_content.duration"
            ]
        )
    },
    RegisteredFixture(id: "swift.integration.rss-feed-items.video") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemFeedResponse.self,
            ignoring: [
                "election_votes.item-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.entity_id",
                "results.published_at",
                "results.story_id",
                "rss_feed_item_elections.item-1.__entity_type",
                "rss_feed_item_elections.item-1.id",
                "rss_feed_items.item-1.__entity_type",
                "rss_feed_items.item-1.data.contentSnippet",
                "rss_feed_items.item-1.data.guid",
                "rss_feed_items.item-1.lingua_rs_detected_language",
                "rss_feed_items.item-1.rss_feed.__entity_type",
                "rss_feed_items.item-1.rss_feed.is_discoverable",
                "rss_feed_items.item-1.rss_feed.is_enabled",
                "rss_feed_items.item-1.rss_feed.last_fetched_at",
                "rss_feed_items.item-1.rss_feed.podcast_show",
                "rss_feed_items.item-1.rss_feed_sources",
                "rss_feed_items.item-1.url.id",
                "rss_feed_items.item-peer.__entity_type",
                "rss_feed_items.item-peer.data.contentSnippet",
                "rss_feed_items.item-peer.data.guid",
                "rss_feed_items.item-peer.lingua_rs_detected_language",
                "rss_feed_items.item-peer.rss_feed.__entity_type",
                "rss_feed_items.item-peer.rss_feed.is_discoverable",
                "rss_feed_items.item-peer.rss_feed.is_enabled",
                "rss_feed_items.item-peer.rss_feed.last_fetched_at",
                "rss_feed_items.item-peer.rss_feed.podcast_show",
                "rss_feed_items.item-peer.rss_feed_sources",
                "rss_feed_items.item-peer.url.id"
            ]
        )
    },
    RegisteredFixture(id: "swift.integration.rss-feed-items.audio") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemFeedResponse.self,
            ignoring: [
                "election_votes.episode-1.__entity_type",
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.entity_id",
                "results.published_at",
                "results.story_id",
                "rss_feed_item_elections.episode-1.__entity_type",
                "rss_feed_item_elections.episode-1.id",
                "rss_feed_item_thumbnail_url",
                "rss_feed_item_thumbnail_url.episode-1",
                "rss_feed_items.episode-1.__entity_type",
                "rss_feed_items.episode-1.data.chapters_type",
                "rss_feed_items.episode-1.data.chapters_url",
                "rss_feed_items.episode-1.data.contentSnippet",
                "rss_feed_items.episode-1.data.enclosure_length",
                "rss_feed_items.episode-1.data.guid",
                "rss_feed_items.episode-1.lingua_rs_detected_language",
                "rss_feed_items.episode-1.rss_feed.__entity_type",
                "rss_feed_items.episode-1.rss_feed.is_discoverable",
                "rss_feed_items.episode-1.rss_feed.is_enabled",
                "rss_feed_items.episode-1.rss_feed.last_fetched_at",
                "rss_feed_items.episode-1.rss_feed.podcast_show.description",
                "rss_feed_items.episode-1.rss_feed.podcast_show.is_explicit",
                "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_author",
                "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_owner_name",
                "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_type",
                "rss_feed_items.episode-1.rss_feed_sources",
                "rss_feed_items.episode-1.url.id"
            ]
        )
    },
    RegisteredFixture(id: "swift.podcast-episode-chapters.default") {
        try assertFixtureCoversDTO($0, as: PodcastChapterResponse.self)
    }
]
