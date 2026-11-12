import Foundation

public extension Endpoint {
    static func rssFeedItems(
        feedType: String = "any",
        after: String? = nil,
        limit: Int = 20,
        mediaType: String? = nil,
        hasHackerNewsReferences: Bool? = nil
    ) -> Endpoint {
        var additional: [URLQueryItem] = []
        if let mediaType {
            additional.append(.init(name: "media_type", value: mediaType))
        }
        if let hasHackerNewsReferences {
            additional.append(.init(
                name: "has_hacker_news_references",
                value: hasHackerNewsReferences ? "true" : "false"
            ))
        }
        return pagedGet(
            path: "/api/v1/feeds/rss_feed_items/\(feedType)",
            after: after,
            limit: limit,
            queryItems: additional
        )
    }

    static func posts(
        feedType: String = "any",
        after: String? = nil,
        limit: Int = 20,
        postTypes: String? = nil,
        timeRange: String? = nil,
        hasHackerNewsReferences: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)"), .init(name: "sort", value: "hot")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let postTypes {
            items.append(.init(name: "post_types", value: postTypes))
        }
        // Omitted by default: the server applies its 30-day window for every sort.
        if let timeRange {
            items.append(.init(name: "time_range", value: timeRange))
        }
        if let hasHackerNewsReferences {
            items.append(.init(name: "has_hacker_news_references", value: hasHackerNewsReferences ? "true" : "false"))
        }
        return Endpoint(.GET, path: "/api/v1/feeds/posts/\(feedType)", queryItems: items)
    }

    /// All RSS feed items — global feed, no personalization scope required.
    static func allRssFeedItems(
        after: String? = nil,
        limit: Int = 20,
        mediaType: String? = nil,
        hasHackerNewsReferences: Bool? = nil
    ) -> Endpoint {
        var additional: [URLQueryItem] = []
        if let mediaType {
            additional.append(.init(name: "media_type", value: mediaType))
        }
        if let hasHackerNewsReferences {
            additional.append(.init(
                name: "has_hacker_news_references",
                value: hasHackerNewsReferences ? "true" : "false"
            ))
        }
        return pagedGet(path: "/api/v1/rss-feed-items", after: after, limit: limit, queryItems: additional)
    }
}
