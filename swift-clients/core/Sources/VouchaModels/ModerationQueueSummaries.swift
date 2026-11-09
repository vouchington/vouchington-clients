import Foundation

public struct ModerationAppealSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let appellantUserId: String
    public let appellant: ReviewDisputeActorSummary
    public let hasDraft: Bool
    public let hasAiDraft: Bool
    @RequiredNullable
    public var recommendedAction: ModerationAppealAction?
    @RequiredNullable
    public var userWarningId: String?
    @RequiredNullable
    public var userSuspensionId: String?
    @RequiredNullable
    public var communityBanId: String?
    @RequiredNullable
    public var postId: String?
    @RequiredNullable
    public var communityId: String?
    @RequiredNullable
    public var postRemovalKind: ModerationAppealPostRemovalKind?
    public let status: ModerationAppealStatus
    @RequiredNullable
    public var publicResponse: String?
    @RequiredNullable
    public var approvedAt: Date?
    @RequiredNullable
    public var sentAt: Date?
    @RequiredNullable
    public var resolvedAt: Date?
    @RequiredNullable
    public var resolutionAction: ModerationAppealAction?
    public let createdAt: Date
    public let updatedAt: Date
    public let isOverdue: Bool?
    public let targetContext: ModerationAppealTargetContext?
}

public struct ReviewDisputeSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String
    public let topicId: String
    public let disputantUserId: String
    public let disputant: ReviewDisputeActorSummary
    public let reason: ReviewDisputeReason
    public let status: ReviewDisputeStatus
    public let hasDraft: Bool
    public let hasAiDraft: Bool
    public let postLabel: String
    @RequiredNullable
    public var postSlug: String?
    @RequiredNullable
    public var recommendedAction: ReviewDisputeAction?
    public let isOverdue: Bool
    @RequiredNullable
    public var publicResponse: String?
    @RequiredNullable
    public var approvedAt: Date?
    @RequiredNullable
    public var sentAt: Date?
    @RequiredNullable
    public var resolvedAt: Date?
    @RequiredNullable
    public var resolutionAction: ReviewDisputeAction?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var postContent: ReviewDisputePostContent?
}
