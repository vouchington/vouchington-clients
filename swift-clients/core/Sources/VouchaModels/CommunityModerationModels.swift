import Foundation

public struct CommunityModeratorStatEntry: Codable, Sendable {
    public let actorUserId: String
    public let total: Int
    public let counts: [String: Int]?
}

public struct CommunityModeratorWorkloadUser: Codable, Sendable {
    public let username: String?
}

public struct CommunityMemberVacation: Codable, Sendable {
    public let communityId: String
    public let userId: String
    public let startsAt: Date
    public let endsAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct CommunityModlogEntry: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let communityId: String?
    public let actorUserId: String?
    public let actionType: String
    public let postId: String?
    public let targetUserId: String?
    public let reportedUserId: String?
    public let reportId: String?
    public let reviewDisputeId: String?
    public let communityApplicationId: String?
    public let reason: String?
    public let metadata: [String: DecodedJSONValue]
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, communityId, actorUserId, actionType, postId, targetUserId, reportedUserId, reportId
        case reviewDisputeId, communityApplicationId, reason, metadata, createdAt
    }
}

public struct CommunityModmailThread: Codable, Identifiable, Sendable {
    public let id: String
    public let channelType: String
    public let title: String
    public let communityId: String
    @RequiredNullable public var subjectUserId: String?
    @RequiredNullable public var assignedModeratorUserId: String?
    @RequiredNullable public var assignedAt: Date?
    @RequiredNullable public var resolvedAt: Date?
    @RequiredNullable public var resolvedById: String?
    @RequiredNullable public var createdById: String?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct CommunityModmailMessage: Codable, Identifiable, Sendable {
    public let id: String
    public let conversationId: String
    public let bodyText: String
    public let createdById: String?
    public let senderUsername: String?
    public let createdAt: Date
    public let updatedAt: Date?
    @RequiredNullable
    public var deletedAt: Date?
}

public struct CommunitySavedReply: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let title: String
    public let body: String
    public let orderIndex: Int
    public let createdById: String?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var deletedAt: Date?
}

public struct CommunityModerationQueueClaim: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let reportId: String?
    public let postId: String?
    public let claimedById: String
    public let claimedAt: Date
    @RequiredNullable
    public var releasedAt: Date?
}

public struct CommunityModerationQueueJudgement: Codable, Sendable {
    public let recommendedAction: String
    public let publicResponse: String
    public let internalResponse: String
    public let isStale: Bool
    public let judgedReportCount: Int?
    public let currentReportCount: Int
}

public struct CommunityModerationQueueEntry: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String?
    public let entityType: String
    public let entityId: String
    public let postId: String?
    public let status: String
    public let queueSource: String
    public let reason: String
    public let note: String?
    public let reportCount: Int
    public let actionAt: Date?
    public let cursorCreatedAt: Date?
    public let createdAt: Date
    public let cursorReportCount: Int?
    public let cursorSeverityRank: Int?
    public let targetAvailable: Bool
    public let targetLabel: String?
    public let targetPath: String?
    @RequiredNullable public var targetContent: AuthoredContentText?
    public let targetIsAnonymous: Bool?
    public let targetIsRestricted: Bool?
    @RequiredNullable
    public var targetUserId: String?
    public let reporterUserId: String?
    @RequiredNullable
    public var reporterUsername: String?
    @RequiredNullable
    public var resolvedById: String?
    @RequiredNullable
    public var judgement: CommunityModerationQueueJudgement?
    public let flaggedReason: String?
    @RequiredNullable
    public var adminActionPath: String?
    @RequiredNullable
    public var communityBanEvasion: CommunityBanEvasionContext?
    @RequiredNullable
    public var postModerationContext: DecodedJSONValue?
    public let isSystemGenerated: Bool
    public let caseId: String?
    @RequiredNullable
    public var reviewedAt: Date?
}

public enum CommunityModerationQueueViewerTier: String, Codable, Sendable {
    case moderator
    case member
}
