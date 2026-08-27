// swiftlint:disable file_length
import Foundation
import VouchaModels

public struct Endpoint: Sendable {
    public let path: String
    public let method: HTTPMethod
    public let queryItems: [URLQueryItem]
    public let headers: [String: String]
    public let body: (any Encodable & Sendable)?
    public let bodyKeyEncodingStrategy: EndpointBodyKeyEncodingStrategy
    private static let pathSegmentAllowedCharacters: CharacterSet = {
        var characters = CharacterSet.urlPathAllowed
        characters.remove(charactersIn: "/:")
        return characters
    }()

    public init(
        _ method: HTTPMethod = .GET,
        path: String,
        queryItems: [URLQueryItem] = [],
        headers: [String: String] = [:],
        body: (any Encodable & Sendable)? = nil,
        bodyKeyEncodingStrategy: EndpointBodyKeyEncodingStrategy = .convertToSnakeCase
    ) {
        self.path = path
        self.method = method
        self.queryItems = queryItems
        self.headers = headers
        self.body = body
        self.bodyKeyEncodingStrategy = bodyKeyEncodingStrategy
    }

    static func pathSegment(_ rawValue: String) -> String {
        guard let encoded = rawValue.addingPercentEncoding(withAllowedCharacters: pathSegmentAllowedCharacters) else {
            preconditionFailure("Unable to percent-encode path segment: \(rawValue)")
        }
        return encoded
    }

    /// Returns a copy of this endpoint with `additionalHeaders` merged in, overriding any
    /// existing header of the same name.
    public func withHeaders(_ additionalHeaders: [String: String]) -> Endpoint {
        Endpoint(
            method,
            path: path,
            queryItems: queryItems,
            headers: headers.merging(additionalHeaders) { _, new in new },
            body: body,
            bodyKeyEncodingStrategy: bodyKeyEncodingStrategy
        )
    }
}

public extension Endpoint {
    static func combinedSearch(query: String, limit: Int = 3) -> Endpoint {
        let items: [URLQueryItem] = [
            .init(name: "q", value: query),
            .init(name: "limit", value: "\(limit)")
        ]
        return Endpoint(.GET, path: "/api/v1/search", queryItems: items)
    }

    static func rssFeedItems(
        feedType: String = "any",
        after: String? = nil,
        limit: Int = 20,
        mediaType: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let mediaType {
            items.append(.init(name: "media_type", value: mediaType))
        }
        return Endpoint(.GET, path: "/api/v1/feeds/rss_feed_items/\(feedType)", queryItems: items)
    }

    static func posts(
        feedType: String = "any",
        after: String? = nil,
        limit: Int = 20,
        postTypes: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)"), .init(name: "sort", value: "hot")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let postTypes {
            items.append(.init(name: "post_types", value: postTypes))
        }
        return Endpoint(.GET, path: "/api/v1/feeds/posts/\(feedType)", queryItems: items)
    }

    static func notifications(after: String? = nil, limit: Int = 20) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/notifications", queryItems: items)
    }

    static var myIdentity: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/identity")
    }

    static var logout: Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/logout")
    }

    static func userFollowing(userId: String, after: String? = nil, limit: Int = 100) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/users/\(pathSegment(userId))/users/following", queryItems: items)
    }

    static func userFollowers(
        userId: String,
        query: String? = nil,
        after: String? = nil,
        limit: Int = 100
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let query {
            items.append(.init(name: "q", value: query))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/users/\(pathSegment(userId))/users/followers", queryItems: items)
    }

    static var myProfile: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/profile")
    }

    static func votePost(postId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/posts/\(pathSegment(postId))/vote", body: ["choice": choice.rawValue])
    }

    static func clearPostVote(postId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/posts/\(pathSegment(postId))/vote")
    }

    static func voteRssFeedItem(rssFeedItemId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/vote",
            body: ["choice": choice.rawValue]
        )
    }

    static func clearRssFeedItemVote(rssFeedItemId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/vote")
    }

    /// Follow a user.
    static func followUser(userId: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/bookmarks/user/\(pathSegment(userId))/follow")
    }

    /// Unfollow a user.
    static func unfollowUser(userId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/bookmarks/user/\(pathSegment(userId))/follow")
    }

    /// Follow an RSS feed.
    static func followRssFeed(rssFeedId: String) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/bookmarks/rss_feed/\(pathSegment(rssFeedId))/follow")
    }

    /// Unfollow an RSS feed.
    static func unfollowRssFeed(rssFeedId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/bookmarks/rss_feed/\(pathSegment(rssFeedId))/follow")
    }

    /// Mark a single notification as read.
    static func markNotificationRead(notificationId: String) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/notifications/\(pathSegment(notificationId))")
    }

    /// Resolve the final navigation target for a notification redirect placeholder.
    static func notificationRedirectTarget(notificationId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/my/notifications/\(pathSegment(notificationId))/redirect-target")
    }

    /// All RSS feed items — global feed, no personalization scope required.
    static func allRssFeedItems(after: String? = nil, limit: Int = 20, mediaType: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let mediaType {
            items.append(.init(name: "media_type", value: mediaType))
        }
        return Endpoint(.GET, path: "/api/v1/rss-feed-items", queryItems: items)
    }

    /// Public RSS feed sources, optionally filtered by feed_type and paginated with a cursor.
    static func allRssFeeds(feedType: String? = nil, after: String? = nil, limit: Int = 100, category: String? = nil)
        -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let feedType {
            items.append(.init(name: "feed_type", value: feedType))
        }
        if let category {
            items.append(.init(name: "category", value: category))
        }
        return Endpoint(.GET, path: "/api/v1/rss-feeds", queryItems: items)
    }

    /// RSS feeds associated with a user (following/subscribed/muted).
    static func userRssFeeds(
        userId: String,
        listType: String = "following",
        feedType: String? = nil,
        after: String? = nil,
        limit: Int = 100
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let feedType {
            items.append(.init(name: "feed_type", value: feedType))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/users/\(pathSegment(userId))/rss-feeds/\(pathSegment(listType))",
            queryItems: items
        )
    }

    /// Mark all notifications as read.
    static var markAllNotificationsRead: Endpoint {
        Endpoint(.POST, path: "/api/v1/my/notifications/read-all")
    }

    /// Update the current user's bio.
    static func updateProfile(markdown: String) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/profile", body: ["markdown": markdown])
    }
}
