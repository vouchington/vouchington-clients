import Foundation

private struct EmptyBody: Encodable {}

public extension Endpoint {
    static func escalateCommunityModerationReport(idOrSlug: String, reportId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/\(pathSegment(reportId))/escalation",
            body: EmptyBody()
        )
    }

    static func deEscalateCommunityModerationReport(idOrSlug: String, reportId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/reports/\(pathSegment(reportId))/escalation"
        )
    }

    static func escalateCommunityPendingPost(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/escalation",
            body: EmptyBody()
        )
    }

    static func deEscalateCommunityPendingPost(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/escalation"
        )
    }
}
