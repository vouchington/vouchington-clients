import Foundation

private struct CreateSourceBody: Encodable {
    let rssFeedUrl: String
    let follow: Bool?
}

public enum RssFeedEnabledFilter: ExpressibleByBooleanLiteral, Equatable, Sendable {
    case enabled
    case disabled
    case all

    public init(booleanLiteral value: Bool) {
        self = value ? .enabled : .disabled
    }

    var queryValue: String {
        switch self {
        case .enabled: "true"
        case .disabled: "false"
        case .all: "null"
        }
    }
}

public extension Endpoint {
    static func rssFeedItem(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/rss-feed-items/\(pathSegment(id))")
    }

    static func rssFeed(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/rss-feeds/\(pathSegment(id))")
    }

    static func rssFeedCrawls(id: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/rss-feeds/\(pathSegment(id))/crawls", queryItems: items)
    }

    static func rssFeedCrawl(id: String, crawlId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/rss-feeds/\(pathSegment(id))/crawls/\(pathSegment(crawlId))")
    }

    static func rssFeeds(
        topicIdentifier: String,
        includeDescendants: Bool? = nil,
        enabled: RssFeedEnabledFilter? = nil,
        discoverable: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "topic", value: topicIdentifier)]
        if let includeDescendants {
            items.append(.init(name: "include_descendants", value: includeDescendants ? "true" : "false"))
        }
        if let enabled {
            items.append(.init(name: "enabled", value: enabled.queryValue))
        }
        if let discoverable {
            items.append(.init(name: "discoverable", value: discoverable ? "true" : "false"))
        }
        return Endpoint(.GET, path: "/api/v1/rss-feeds", queryItems: items)
    }

    static func createSource(rssFeedURL: String, follow: Bool? = nil) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feeds", body: CreateSourceBody(rssFeedUrl: rssFeedURL, follow: follow))
    }

    static func updateRssFeed(rssFeedId: String, body: some Encodable & Sendable) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/rss-feeds/\(pathSegment(rssFeedId))", body: body)
    }

    static func deleteRssFeed(rssFeedId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/rss-feeds/\(pathSegment(rssFeedId))")
    }

    static func refreshRssFeed(rssFeedId: String, body: some Encodable & Sendable) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feeds/\(pathSegment(rssFeedId))/refreshes", body: body)
    }

    static func shareRssFeedItemWithFollowers(rssFeedItemId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/shares")
    }

    static func sendRssFeedItemToFollowers(
        rssFeedItemId: String,
        request: FollowerDistributionRequest
    ) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/sends", body: request)
    }
}
