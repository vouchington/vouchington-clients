import Foundation

public extension Endpoint {
    static func friendRecommendations(after: String? = nil, limit: Int = 25) -> Endpoint {
        pagedGet(path: "/api/v1/my/friend-recommendations", after: after, limit: limit)
    }
}
