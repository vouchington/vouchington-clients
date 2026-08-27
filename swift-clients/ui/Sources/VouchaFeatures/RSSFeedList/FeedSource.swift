/// Which feed to load: the user's personalized feed or the global firehose.
public enum FeedSource: Sendable {
    /// Personalized feed: union of all your follows.
    /// Uses `GET /api/v1/feeds/rss_feed_items/any`.
    case your
    /// Global feed: all items regardless of follow graph.
    /// Uses `GET /api/v1/rss-feed-items`.
    case all
}
