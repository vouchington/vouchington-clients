import VouchaModels

/// Null-valued optional fields are omitted when a DTO re-encodes, so they are ignored per post id.
private func postNullFields(_ ids: [String]) -> Set<String> {
    Set(ids.flatMap { id in
        ["clearance_status", "slug", "created_by", "created_by_id"].map { "posts.\(id).\($0)" }
    })
}

private let threadNullFields: Set<String> = [
    "page_info.end_cursor",
    "page_info.start_cursor",
    "results.__entity_type",
    "results.id",
    "results.ranking",
    "results.search_vector_ts"
]

private func threadPage(_ id: String, posts: [String]) -> RegisteredFixture {
    RegisteredFixture(id: id) {
        try assertFixtureCoversDTO(
            $0,
            as: PostThreadEnvelope.self,
            ignoring: threadNullFields.union(postNullFields(posts))
        )
    }
}

private func postFeed(_ id: String) -> RegisteredFixture {
    RegisteredFixture(id: id) {
        try assertFixtureCoversDTO(
            $0,
            as: UserProfilePostFeedResponse.self,
            ignoring: ["bookmarks", "communities", "results.__entity_type"]
        )
    }
}

private let rssFeedSidecarFields: Set<String> = [
    "posts", "posts_metrics", "related_posts_by_url_id", "stories", "users",
    "results.__entity_type", "results.delivery_type", "results.entity_id",
    "results.published_at", "results.story_id", "rss_feed_item_thumbnail_url"
]

private func rssItemFeed(_ id: String, items: [String], extra: Set<String> = []) -> RegisteredFixture {
    let languages = Set(items.map { "rss_feed_items.\($0).lingua_rs_detected_language" })
    return RegisteredFixture(id: id) {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemFeedResponse.self,
            ignoring: rssFeedSidecarFields.union(languages).union(extra)
        )
    }
}

private func personalFeed(_ id: String, _ suffixes: [String]) -> RegisteredFixture {
    rssItemFeed(id, items: suffixes.map { "70000000-0000-7000-8000-0000000054" + $0 })
}

private let threadPrefix = "70000000-0000-7000-8000-0000000054"

let nativeContentFixtureCoverage: [RegisteredFixture] = [
    threadPage("native.comments.descendants.default", posts: ["01", "02", "03"].map { threadPrefix + $0 }),
    threadPage("native.comments.descendants.short-continuation", posts: ["31", "34"].map { threadPrefix + $0 }),
    threadPage("native.comments.descendants.empty-continuation", posts: ["33", "35"].map { threadPrefix + $0 }),
    threadPage("native.comments.descendants.after-filtered-head", posts: ["36", "37"].map { threadPrefix + $0 }),
    threadPage("native.comments.descendants.expand-preview-child", posts: ["37", "38"].map { threadPrefix + $0 }),
    RegisteredFixture(id: "native.posts.hacker-news.detail") {
        try assertFixtureCoversDTO(
            $0,
            as: PostEnvelope.self,
            ignoring: ["author_aside.lingua_rs_detected_language", "communities", "post.clearance_status"]
        )
    },
    RegisteredFixture(id: "native.rss-feed-items.hacker-news.detail") {
        try assertFixtureCoversDTO(
            $0,
            as: RssFeedItemDetailResponse.self,
            ignoring: ["rss_feed_item.lingua_rs_detected_language"]
        )
    },
    postFeed("native.posts.hacker-news.filtered"),
    postFeed("native.posts.time-range.all"),
    postFeed("native.posts.time-range.explicit-week"),
    rssItemFeed("native.rss-feed-items.hacker-news.list", items: ["item-1"], extra: ["bookmarks"]),
    rssItemFeed(
        "native.podcast-episodes.hacker-news.list",
        items: ["episode-1"],
        extra: [
            "rss_feed_item_thumbnail_url.episode-1",
            "rss_feed_items.episode-1.data.chapters_type",
            "rss_feed_items.episode-1.data.chapters_url",
            "rss_feed_items.episode-1.data.enclosure_length",
            "rss_feed_items.episode-1.rss_feed.podcast_show.description",
            "rss_feed_items.episode-1.rss_feed.podcast_show.is_explicit",
            "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_author",
            "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_owner_name",
            "rss_feed_items.episode-1.rss_feed.podcast_show.itunes_type"
        ]
    ),
    personalFeed("native.rss-personal.one-day", ["61"]),
    personalFeed("native.rss-personal.seven-day.1m", ["61", "62", "63"]),
    personalFeed("native.rss-personal.seven-day.1y", ["61", "62", "63"]),
    personalFeed("native.rss-personal.seven-day.all", ["61", "62", "63"]),
    personalFeed("native.rss-personal.union.first-page", ["61", "62"]),
    personalFeed("native.rss-personal.union.next-page", ["63"])
] + nativeLifecycleFixtureCoverage

private let nullTimestampFields: Set<String> = [
    "import.completed_at",
    "topic_merge.completed_at",
    "topic_merge.error_code"
]

private func lifecycle(_ id: String, as type: (some Codable).Type) -> RegisteredFixture {
    RegisteredFixture(id: id) {
        try assertFixtureCoversDTO($0, as: type, ignoring: nullTimestampFields)
    }
}

private let nativeLifecycleFixtureCoverage: [RegisteredFixture] = [
    lifecycle("native.rss-feeds.create.accepted", as: RssFeedImportSubmission.self),
    lifecycle("native.rss-feeds.create.status.pending", as: RssFeedImportStatus.self),
    lifecycle("native.rss-feeds.create.status.completed", as: RssFeedImportStatus.self),
    lifecycle("native.rss-feeds.create.status.failed", as: RssFeedImportStatus.self),
    lifecycle("native.rss-feeds.delete.accepted", as: DecodedJSONValue.self),
    lifecycle("native.rss-feeds.delete.retained-import-conflict", as: DecodedJSONValue.self),
    lifecycle("native.topics.merge.accepted", as: TopicMergeEnvelope.self),
    lifecycle("native.topics.merge.status.queued", as: TopicMergeEnvelope.self),
    lifecycle("native.topics.merge.status.running", as: TopicMergeEnvelope.self),
    lifecycle("native.topics.merge.status.completed", as: TopicMergeEnvelope.self),
    lifecycle("native.topics.merge.status.failed", as: TopicMergeEnvelope.self)
]
