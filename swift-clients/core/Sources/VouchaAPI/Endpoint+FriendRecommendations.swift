import Foundation

public extension Endpoint {
    static func friendRecommendations(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/my/friend-recommendations",
            queryItems: queryItems
        )
    }
}
