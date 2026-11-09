import Foundation

public extension Endpoint {
    static func pendingTopicClaims() -> Endpoint {
        Endpoint(.GET, path: "/api/v1/admin/topic-claims")
    }

    static func verifyTopicClaim(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/admin/topic-claims/\(pathSegment(id))/verification")
    }

    static func rejectTopicClaim(id: String, reason: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/admin/topic-claims/\(pathSegment(id))/rejection",
            body: ["rejection_reason": reason]
        )
    }

    static func revokeTopicClaim(id: String, reason: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/admin/topic-claims/\(pathSegment(id))/revocation",
            body: ["revocation_reason": reason]
        )
    }

    static func userModerationNotes(userId: String, after: String? = nil, limit: Int = 20) -> Endpoint {
        pagedGet(path: "/api/v1/users/\(pathSegment(userId))/mod-notes", after: after, limit: limit)
    }

    static func createUserModerationNote(userId: String, body: String, communityId: String? = nil) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/users/\(pathSegment(userId))/mod-notes",
            body: UserModerationNoteBody(body: body, communityId: communityId)
        )
    }
}

private struct UserModerationNoteBody: Encodable {
    let body: String
    let communityId: String?

    enum CodingKeys: String, CodingKey {
        case body
        case communityId = "community_id"
    }
}
