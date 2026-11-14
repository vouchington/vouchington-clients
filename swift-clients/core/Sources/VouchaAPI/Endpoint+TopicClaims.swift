import Foundation

extension Endpoint {
    static func myTopicClaims(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(URLQueryItem(name: "after", value: after))
        }
        items.append(URLQueryItem(name: "limit", value: String(limit)))
        return Endpoint(.GET, path: "/api/v1/my/topic-claims", queryItems: items)
    }

    static func topicClaims(
        idOrSlug: String,
        mineOnly: Bool? = nil,
        activeOnly: Bool? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items: [URLQueryItem] = []
        if let after {
            items.append(URLQueryItem(name: "after", value: after))
        }
        items.append(URLQueryItem(name: "limit", value: String(limit)))
        if let mineOnly {
            items.append(URLQueryItem(name: "mine_only", value: mineOnly ? "true" : "false"))
        }
        if let activeOnly {
            items.append(URLQueryItem(name: "active_only", value: activeOnly ? "true" : "false"))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/topics/\(Self.pathSegment(idOrSlug))/claims",
            queryItems: items
        )
    }
}
