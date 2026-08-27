import Foundation

public struct MemberWarningNotice: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String?
    public let userId: String
    public let communityId: String?
    public let communitySlug: String?
    public let publicMessage: String?
    public let revokedAt: Date?
    public let createdAt: Date
}

public struct MemberWarningNoticesResponse: Codable, Sendable {
    public let warnings: [MemberWarningNotice]
    public let pageInfo: Page<MemberWarningNotice>.PageInfo
}

public struct MemberCommunityBanNotice: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let communityId: String
    @RequiredNullable
    public var communitySlug: String?
    @RequiredNullable
    public var reason: String?
    @RequiredNullable
    public var expiresAt: Date?
    @RequiredNullable
    public var liftedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct MemberCommunityBanNoticesResponse: Codable, Sendable {
    public let bans: [MemberCommunityBanNotice]
    public let pageInfo: Page<MemberCommunityBanNotice>.PageInfo
}

public struct MemberRemovedPostNotice: Codable, Identifiable, Sendable {
    public var id: String {
        "\(postRemovalKind.rawValue):\(postId)"
    }

    public let entityType: String
    public let postId: String
    @RequiredNullable
    public var communityId: String?
    @RequiredNullable
    public var communitySlug: String?
    @RequiredNullable
    public var postTitle: String?
    public let postRemovalKind: ModerationAppealPostRemovalKind
    public let unpublishedAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case postId, communityId, communitySlug, postTitle, postRemovalKind, unpublishedAt
    }
}

public struct MemberRemovedPostNoticesResponse: Codable, Sendable {
    public let removedPosts: [MemberRemovedPostNotice]
    public let pageInfo: Page<MemberRemovedPostNotice>.PageInfo
}
