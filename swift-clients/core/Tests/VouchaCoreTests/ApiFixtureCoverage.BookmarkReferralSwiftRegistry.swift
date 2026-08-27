import VouchaModels

let bookmarkReferralSwiftCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.topic-recommendation.detail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: PostEnvelope.self,
            ignoring: [
                "bookmarks",
                "election_vote", "election_vote.__entity_type",
                "post.html",
                "post.parent_id",
                "post.slug",
                "post.topic_recommendation",
                "post.topic_recommendation.aliases",
                "post.topic_recommendation.approval_error_message",
                "post.topic_recommendation.created_topic_id",
                "post.topic_recommendation.created_topic_slug",
                "post.topic_recommendation.example_referral_link",
                "post.topic_recommendation.hostname",
                "post.topic_recommendation.hostname_id",
                "post.topic_recommendation.hostnames",
                "post.topic_recommendation.landing_page_urls",
                "post.topic_recommendation.post_id",
                "post.topic_recommendation.rejection_reason",
                "post.topic_recommendation.reviewed_at",
                "post.topic_recommendation.reviewed_by_id",
                "post.topic_recommendation.status",
                "post.topic_recommendation.topic_markdown",
                "post.topic_recommendation.topic_slug",
                "post.topic_recommendation.topic_title",
                "post.topic_recommendation.topic_type",
                "post.topic_recommendation.topic_wikipedia_pageid",
                "post_election", "post_election.__entity_type", "post_election.id",
                "post_metrics", "post_metrics.__entity_type",
                "post_metrics.bookmarks",
                "post_metrics.bookmarks.follow",
                "post_metrics.bookmarks.save",
                "post_metrics.id",
                "post_metrics.updated_at"
            ]
        )
    },
    RegisteredFixture(id: "native.rss-feed-item.detail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemDetailResponse.self,
            ignoring: [
                "election_vote.__entity_type",
                "rss_feed_item.lingua_rs_detected_language",
                "rss_feed_item.rss_feed_sources",
                "rss_feed_item_election.__entity_type",
                "rss_feed_item_election.id"
            ]
        )
    },
    RegisteredFixture(id: "native.bookmarks.posts.saved.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Post>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.topics.muted.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Topic>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.topics.viewed.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Topic>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.users.subscribed-posts.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.users.dismissed-recommendations.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<PublicUser>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.rss-feed-items.saved.default") {
        try assertFixtureCoversDTO(
            $0,
            as: BookmarkRssFeedItemsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.rss-feeds.muted.default") {
        try assertFixtureCoversDTO(
            $0,
            as: BookmarkRssFeedsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.rss-feeds.viewed.default") {
        try assertFixtureCoversDTO(
            $0,
            as: BookmarkRssFeedsResponse.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.urls.saved.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Url>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.domains.blocked.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Hostname>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.domains.muted.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Hostname>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor"]
        )
    },
    RegisteredFixture(id: "native.bookmarks.communities.proxy-following.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<Community>.self,
            ignoring: ["page_info.end_cursor", "page_info.start_cursor", "results.owner"]
        )
    },
    RegisteredFixture(id: "web.topics.search.referral-programs.default") {
        try assertFixtureCoversDTO(
            $0,
            as: TopicSearchResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "topics.referral-program-1.__entity_type",
                "topics.referral-program-1.created_by",
                "topics.referral-program-1.created_by.id",
                "topics.referral-program-1.created_by.username",
                "topics.referral-program-1.hero_image_id",
                "topics.referral-program-1.homepage_url_id",
                "topics.referral-program-1.hostname",
                "topics.referral-program-1.hostname_id",
                "topics.referral-program-1.lingua_rs_detected_language",
                "topics.referral-program-1.logo_image_id",
                "topics.referral-program-1.referral_program_id",
                "topics.referral-program-1.referral_program_slug",
                "topics.referral-program-1.rewards_program_id",
                "topics.referral-program-1.updated_by",
                "topics.referral-program-1.updated_by.id",
                "topics.referral-program-1.updated_by.username"
            ]
        )
    },
    RegisteredFixture(id: "web.referral-links.feed.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReferralLinkFeedResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "native.referral-links.mine.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<NativeReferralLink>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.deactivated_at",
                "results.deleted_at",
                "results.last_crawl_failure_at",
                "results.last_crawl_id",
                "results.last_crawl_success_at",
                "results.parent_link_id",
                "results.unfurl_completed_at",
                "results.unfurl_failed_at",
                "results.unfurl_last_error",
                "results.unfurl_requested_at"
            ]
        )
    },
    RegisteredFixture(id: "native.referral-clicks.mine.default") {
        try assertFixtureCoversDTO(
            $0,
            as: ReferralClickLogResponse.self,
            ignoring: [
                "clicks.referral-click-1.__entity_type",
                "clicks.referral-click-2.__entity_type",
                "clicks.referral-click-2.signed_up_at",
                "clicks.referral-click-2.user_id",
                "page_info.end_cursor",
                "results.__entity_type",
                "users.user-2.__entity_type",
                "users.user-2.profile_image_id"
            ]
        )
    },
    RegisteredFixture(id: "web.trending-referral-programs.default") {
        try assertFixtureCoversDTO(
            $0,
            as: TrendingReferralProgramsResponse.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor"
            ]
        )
    },
    RegisteredFixture(id: "web.referral-links.prioritized.default") {
        try assertFixtureCoversDTO($0, as: PrioritizedReferralLinksResponse.self)
    }
]
