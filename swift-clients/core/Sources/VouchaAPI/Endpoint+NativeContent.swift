import Foundation

public extension Endpoint {
    /// `GET /api/v1/posts` with the time-range and Hacker News reference filters.
    static func postsFeed(
        postTypes: String? = nil,
        timeRange: String? = nil,
        hasHackerNewsReferences: Bool? = nil,
        sort: String? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let sort {
            items.append(.init(name: "sort", value: sort))
        }
        if let postTypes {
            items.append(.init(name: "post_types", value: postTypes))
        }
        if let timeRange {
            items.append(.init(name: "time_range", value: timeRange))
        }
        if let hasHackerNewsReferences {
            items.append(.init(name: "has_hacker_news_references", value: hasHackerNewsReferences ? "true" : "false"))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/posts", queryItems: items)
    }

    /// The signed-in member's personalised RSS feed, bounded by a time range.
    static func personalRssFeedItems(timeRange: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        pagedGet(
            path: "/api/v1/feeds/rss_feed_items/all",
            after: after,
            limit: limit,
            queryItems: [.init(name: "time_range", value: timeRange)]
        )
    }

    static func topicMergeStatus(sourceTopicId: String, operationId: String) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/topics/\(pathSegment(sourceTopicId))/merges/\(pathSegment(operationId))"
        )
    }
}
