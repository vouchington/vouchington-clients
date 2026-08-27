import Foundation
import VouchaModels

private struct CommunityMemberRoleBody: Encodable {
    let role: CommunityMemberRole
}

private struct CommunityOwnershipTransferBody: Encodable {
    let userId: String
}

private struct CommunityModerationStatusBody: Encodable {
    let status: String
    let reason: String?
}

private struct CommunityBanBody: Encodable {
    let userId: String
    let reason: String?
    let expiresAt: Date?
}

private struct CommunityRestrictionActivationBody: Encodable {
    let restrictionTypes: [CommunityRestrictionType]
    let expiresAt: Date?
    let reason: String?
}

private struct CommunityListItemBody: Encodable {
    let entityType: CommunityListItemType
    let entityId: String

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch entityType {
        case .topic:
            try container.encode(entityId, forKey: .topicId)
        case .rssFeed:
            try container.encode(entityId, forKey: .rssFeedId)
        case .post:
            try container.encode(entityId, forKey: .postId)
        case .urlHostname:
            try container.encode(entityId, forKey: .urlHostnameId)
        case .url:
            try container.encode(entityId, forKey: .urlId)
        }
    }

    private enum CodingKeys: String, CodingKey {
        case topicId = "topic_id"
        case rssFeedId = "rss_feed_id"
        case postId = "post_id"
        case urlHostnameId = "url_hostname_id"
        case urlId = "url_id"
    }
}

public extension Endpoint {
    static func updateCommunityMemberRole(idOrSlug: String, userId: String, role: CommunityMemberRole) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/members/\(pathSegment(userId))",
            body: CommunityMemberRoleBody(role: role)
        )
    }

    static func removeCommunityMember(idOrSlug: String, userId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/members/\(pathSegment(userId))")
    }

    static func transferCommunityOwnership(idOrSlug: String, userId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/ownership-transfers",
            body: CommunityOwnershipTransferBody(userId: userId)
        )
    }

    static func addCommunityListItem(idOrSlug: String, itemType: CommunityListItemType, entityId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/list-items/\(itemType.routeSegment)",
            body: CommunityListItemBody(entityType: itemType, entityId: entityId)
        )
    }

    static func addCommunityListTopic(idOrSlug: String, topicId: String) -> Endpoint {
        addCommunityListItem(idOrSlug: idOrSlug, itemType: .topic, entityId: topicId)
    }

    static func addCommunityListRssFeed(idOrSlug: String, rssFeedId: String) -> Endpoint {
        addCommunityListItem(idOrSlug: idOrSlug, itemType: .rssFeed, entityId: rssFeedId)
    }

    static func addCommunityListPost(idOrSlug: String, postId: String) -> Endpoint {
        addCommunityListItem(idOrSlug: idOrSlug, itemType: .post, entityId: postId)
    }

    static func addCommunityListDomain(idOrSlug: String, urlHostnameId: String) -> Endpoint {
        addCommunityListItem(idOrSlug: idOrSlug, itemType: .urlHostname, entityId: urlHostnameId)
    }

    static func addCommunityListUrl(idOrSlug: String, urlId: String) -> Endpoint {
        addCommunityListItem(idOrSlug: idOrSlug, itemType: .url, entityId: urlId)
    }

    static func removeCommunityListItem(idOrSlug: String, itemType: CommunityListItemType, itemId: String) -> Endpoint {
        let path = "/api/v1/communities/\(pathSegment(idOrSlug))"
        return Endpoint(
            .DELETE,
            path: "\(path)/list-items/\(itemType.routeSegment)/\(pathSegment(itemId))"
        )
    }

    static func approveCommunityPostReview(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))",
            body: CommunityModerationStatusBody(status: "approved", reason: nil)
        )
    }

    static func rejectCommunityPostReview(idOrSlug: String, postId: String, rejectionReason: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))",
            body: CommunityModerationStatusBody(status: "rejected", reason: rejectionReason)
        )
    }

    static func unpublishCommunityPostReview(idOrSlug: String, postId: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/posts/\(pathSegment(postId))",
            body: CommunityModerationStatusBody(status: "unpublished", reason: nil)
        )
    }

    static func banCommunityMember(
        idOrSlug: String,
        userId: String,
        reason: String? = nil,
        expiresAt: Date? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans",
            body: CommunityBanBody(userId: userId, reason: reason, expiresAt: expiresAt)
        )
    }

    static func liftCommunityBan(idOrSlug: String, userId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/communities/\(pathSegment(idOrSlug))/bans/\(pathSegment(userId))")
    }

    static func activateCommunityRestrictions(
        idOrSlug: String,
        restrictionTypes: [CommunityRestrictionType],
        expiresAt: Date? = nil,
        reason: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/restrictions",
            body: CommunityRestrictionActivationBody(
                restrictionTypes: restrictionTypes,
                expiresAt: expiresAt,
                reason: reason
            )
        )
    }

    static func liftCommunityRestriction(idOrSlug: String, restrictionId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/restrictions/\(pathSegment(restrictionId))"
        )
    }

    static func communityModeratorStats(idOrSlug: String, window: Int = 30) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/communities/\(pathSegment(idOrSlug))/moderator-stats",
            queryItems: [.init(name: "window", value: "\(window)")]
        )
    }

}
