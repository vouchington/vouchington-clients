import Foundation

public struct CommunityAiAgentEntitlement: Codable, Sendable {
    public let allowed: Bool
    public let reason: String?
}

public enum CommunityOnFlagAction: String, Codable, Sendable {
    case none
    case reviewQueue = "review_queue"
    case unpublish
}

public struct CommunityAiAgent: Codable, Sendable {
    public let slug: String
    public let agentId: String
    public let systemUserId: String
    public let systemUsername: String
    public let labelTopicSlugs: [String]
    public let onFlagAction: CommunityOnFlagAction
    public let enabled: Bool
    public let alwaysOn: Bool
    public let enabledAt: Date?
    public let enabledById: String?
    public let entitlement: CommunityAiAgentEntitlement?
}

public struct CommunityBan: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String?
    public let communityId: String
    public let userId: String
    public let bannedById: String?
    public let reason: String?
    public let expiresAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    public let liftedAt: Date?
    public let liftedById: String?
}

public struct CommunityBanEvasionContext: Codable, Sendable {
    public let communityId: String
    public let communitySlug: String
    public let sourceUserId: String
    public let sourceUsername: String?
    public let score: Double
    public let flaggedAt: Date
}

public struct CommunityRestriction: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let restrictionType: CommunityRestrictionType
    public let activatedById: String?
    public let activatedAt: Date
    public let expiresAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    public let liftedAt: Date?
    public let liftedById: String?
    public let reason: String?
}
