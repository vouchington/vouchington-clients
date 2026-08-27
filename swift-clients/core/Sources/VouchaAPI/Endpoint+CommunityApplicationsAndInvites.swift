import Foundation

private struct CommunityModerationStatusBody: Encodable {
    let status: String
    let reason: String?
}

private struct CommunityInviteBody: Encodable {
    let email: String?
    let username: String?
}

public extension Endpoint {
    static func approveCommunityApplication(idOrSlug: String, applicationId: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/applications/\(pathSegment(applicationId))",
            body: CommunityModerationStatusBody(status: "approved", reason: nil)
        )
    }

    static func rejectCommunityApplication(
        idOrSlug: String,
        applicationId: String,
        rejectionReason: String
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/applications/\(pathSegment(applicationId))",
            body: CommunityModerationStatusBody(status: "rejected", reason: rejectionReason)
        )
    }

    static func sendCommunityInvite(idOrSlug: String, email: String? = nil, username: String? = nil) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/invites",
            body: CommunityInviteBody(email: email, username: username)
        )
    }

    static func communityInvites(idOrSlug: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/communities/\(pathSegment(idOrSlug))/invites", queryItems: items)
    }

    static func revokeCommunityInvite(idOrSlug: String, inviteId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/invites/\(pathSegment(inviteId))")
    }

    static func redeemCommunityInviteCode(code: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/communities/invite-redemptions", body: ["code": code])
    }
}
