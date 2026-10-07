import VouchaModels

let userProfileApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.users.profile.default") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfileResponse.self,
            ignoring: userProfileIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.profile.restricted") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfileResponse.self,
            ignoring: userProfileIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.vouch-context.default") {
        try assertFixtureCoversDTO($0, as: UserTrustContext.self)
    },
    RegisteredFixture(id: "native.users.profile.posts.all") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfilePostFeedResponse.self,
            ignoring: profilePostFeedIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.profile.posts.reviews") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfilePostFeedResponse.self,
            ignoring: profilePostFeedIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.profile.posts.discussions") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfilePostFeedResponse.self,
            ignoring: profilePostFeedIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.profile.posts.comments") {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfilePostFeedResponse.self,
            ignoring: profilePostFeedIgnoredFields
        )
    },
    RegisteredFixture(id: "native.users.profile.topics-following.first-page") {
        try assertFixtureCoversDTO($0, as: Page<Topic>.self)
    },
    RegisteredFixture(id: "native.users.profile.topics-following.next-page") {
        try assertFixtureCoversDTO($0, as: Page<Topic>.self)
    },
    RegisteredFixture(id: "native.users.profile.sources-following.article") {
        try assertFixtureCoversDTO($0, as: RssFeedSourceListResponse.self)
    },
    RegisteredFixture(id: "native.users.profile.communities-member.first-page") {
        try assertFixtureCoversDTO($0, as: Page<Community>.self, ignoring: ["results.owner"])
    },
    RegisteredFixture(id: "native.users.profile.communities-member.next-page") {
        try assertFixtureCoversDTO($0, as: Page<Community>.self)
    }
]

private let userProfileIgnoredFields: Set<String> = [
    "profile_links.handle", "profile_links.image_id", "profile_links.url_id",
    "user.cards_visibility", "user.community_digest_frequency",
    "user.community_memberships_visibility", "user.default_post_broadcast",
    "user.default_post_privacy", "user.direct_messages_audience",
    "user.is_engagement_emails_enabled", "user.is_fediverse_federation_enabled",
    "user.followers_visibility", "user.follows_visibility", "user.likes_visibility",
    "user.moderation_email_cadence", "user.moderation_email_days_of_week",
    "user.moderation_email_time_of_day", "user.moderation_email_timezone",
    "user.is_moderation_emails_enabled", "user.news_digest_frequency",
    "user.rewards_program_statuses_visibility", "user.rss_feed_follows_visibility",
    "user.spending_categories_visibility", "user.topic_follows_visibility",
    "user_metrics.__entity_type", "user_metrics.bookmarks",
    "user_metrics.bookmarks.follow", "user_metrics.bookmarks.follow.posts",
    "user_metrics.bookmarks.follow.topics", "user_metrics.bookmarks.follow.users",
    "user_metrics.bookmarks__updated_at"
]

private let profilePostFeedIgnoredFields: Set<String> = [
    "bookmarks",
    "communities",
    "posts.profile-comment-1.broadcast",
    "posts.profile-comment-1.clearance_status",
    "posts.profile-comment-1.html",
    "posts.profile-comment-1.slug",
    "posts.profile-comment-1.title",
    "posts.profile-comment-root-1.broadcast",
    "posts.profile-comment-root-1.clearance_status",
    "posts.profile-comment-root-1.html",
    "posts.profile-comment-root-1.markdown",
    "posts.profile-comment-root-1.parent_id",
    "posts.profile-discussion-1.broadcast",
    "posts.profile-discussion-1.clearance_status",
    "posts.profile-discussion-1.html",
    "posts.profile-discussion-1.markdown",
    "posts.profile-discussion-1.parent_id",
    "posts.profile-review-1.broadcast",
    "posts.profile-review-1.clearance_status",
    "posts.profile-review-1.html",
    "posts.profile-review-1.markdown",
    "posts.profile-review-1.parent_id",
    "results.__entity_type"
]
