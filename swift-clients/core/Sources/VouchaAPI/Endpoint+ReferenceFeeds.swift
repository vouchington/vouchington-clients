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
