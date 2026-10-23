import Foundation

extension Endpoint {
    static func myTopicClaims(limit: Int = 25) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/my/topic-claims",
            queryItems: [URLQueryItem(name: "limit", value: String(limit))]
        )
    }

    static func topicClaims(
        idOrSlug: String,
        mineOnly: Bool? = nil,
        activeOnly: Bool? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var items = [URLQueryItem(name: "limit", value: String(limit))]
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
