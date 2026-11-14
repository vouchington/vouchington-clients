import Foundation

public extension Endpoint {
    static func userModerationContext(userId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/users/\(pathSegment(userId))/moderation-context")
    }
}
