import Foundation

public extension Endpoint {
    static func story(
        storyId: String,
        after: String? = nil,
        excludeItemId: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var query = ["limit": String(limit)]
        if let after { query["after"] = after }
        if let excludeItemId { query["exclude_item_id"] = excludeItemId }
        return Endpoint(
            .GET,
            path: "/api/v1/stories/\(pathSegment(storyId))",
            queryItems: query.map { URLQueryItem(name: $0.key, value: $0.value) }
        )
    }
}
