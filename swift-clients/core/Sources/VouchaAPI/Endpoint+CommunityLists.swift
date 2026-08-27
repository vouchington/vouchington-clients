import Foundation

public extension Endpoint {
    static func communityBans(idOrSlug: String, after: String? = nil, limit: Int? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        if let limit {
            items.append(.init(name: "limit", value: "\(limit)"))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans", queryItems: items)
    }

    static func communityRestrictions(idOrSlug: String, after: String? = nil) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/restrictions", queryItems: items)
    }
}
