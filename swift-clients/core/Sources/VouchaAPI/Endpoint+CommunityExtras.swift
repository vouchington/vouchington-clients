import Foundation

private struct CreateCommunityWarningBody: Encodable {
    let userId: String
    let reason: String
    let publicMessage: String?
    let reportId: String?
    let resolveReport: Bool?
}

private struct CommunityPostTypeSettingsBody: Encodable {
    let allowReviewPosts: Bool
    let allowDataPointPosts: Bool
}

public extension Endpoint {
    static func communityModerationResults(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))/moderation-results"
        )
    }

    static func createCommunityWarning(
        idOrSlug: String,
        userId: String,
        reason: String,
        publicMessage: String? = nil,
        reportId: String? = nil,
        resolveReport: Bool? = nil
    ) -> Endpoint {
        let body = CreateCommunityWarningBody(
            userId: userId,
            reason: reason,
            publicMessage: publicMessage,
            reportId: reportId,
            resolveReport: resolveReport
        )
        return Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/warnings",
            body: body,
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func updateCommunityPostTypeSettings(
        idOrSlug: String,
        allowReviewPosts: Bool,
        allowDataPointPosts: Bool
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/post-type-settings",
            body: CommunityPostTypeSettingsBody(
                allowReviewPosts: allowReviewPosts,
                allowDataPointPosts: allowDataPointPosts
            )
        )
    }
}
