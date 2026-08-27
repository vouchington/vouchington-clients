import Foundation
import VouchaModels

public extension Endpoint {
    static func communityMembers(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 25,
        role: CommunityMemberRole? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let role {
            items.append(.init(name: "role", value: role.rawValue))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/members", queryItems: items)
    }

    static func communityPosts(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 25,
        sort: String? = nil,
        query: String? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let sort {
            items.append(.init(name: "sort", value: sort))
        }
        if let query {
            items.append(.init(name: "q", value: query))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts", queryItems: items)
    }

    static func communityNews(
        idOrSlug: String,
        after: String? = nil,
        limit: Int = 25,
        feedType: String? = nil,
        query: String? = nil,
        hasRelatedPosts: Bool? = nil
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let feedType {
            items.append(.init(name: "feed_type", value: feedType))
        }
        if let query {
            items.append(.init(name: "q", value: query))
        }
        if let hasRelatedPosts {
            items.append(.init(
                name: "has_related_posts",
                value: hasRelatedPosts ? "true" : "false"
            ))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/news", queryItems: items)
    }

    static func communityListItemCounts(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/list-items/counts")
    }

    static func communityListItems(
        idOrSlug: String,
        itemType: CommunityListItemType,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/list-items/\(itemType.routeSegment)",
            queryItems: items
        )
    }
}
