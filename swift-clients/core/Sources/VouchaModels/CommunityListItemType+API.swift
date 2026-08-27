public extension CommunityListItemType {
    var requestBodyField: String {
        switch self {
        case .topic:
            "topic_id"
        case .rssFeed:
            "rss_feed_id"
        case .post:
            "post_id"
        case .urlHostname:
            "url_hostname_id"
        case .url:
            "url_id"
        }
    }
}
