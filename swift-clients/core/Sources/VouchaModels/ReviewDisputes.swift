import Foundation

public enum IntegrityFlagStatus: String, Codable, Sendable {
    case pending, resolved
}

public enum ReviewDisputeReason: String, Codable, Sendable {
    case factuallyInaccurate = "factually_inaccurate"
    case defamatory
    case impersonation
    case privacyViolation = "privacy_violation"
    case other
}

public enum ReviewDisputeStatus: String, Codable, Sendable {
    case pending, resolved, dismissed
}

public enum ReviewDisputeAction: String, Codable, Sendable {
    case noAction = "no_action"
    case remove, annotate, dismiss
}

public enum ReviewDisputeResolutionAction: String, Codable, Sendable {
    case remove, annotate, dismiss
}

public struct ReviewDisputeActorSummary: Codable, Sendable {
    public let id: String
    @RequiredNullable
    public var username: String?
    @RequiredNullable
    public var verifiedDisplayName: String?
    @RequiredNullable
    public var profileImageId: String?
}

public struct ReviewDisputePostContext: Codable, Sendable {
    public let id: String
    public let title: String
    @RequiredNullable public var declaredLanguage: String?
    @RequiredNullable public var linguaRsDetectedLanguage: String?
    @RequiredNullable
    public var slug: String?
    public let markdownPreview: String
    @RequiredNullable
    public var createdById: String?
    public let createdAt: Date
}

public struct ReviewDisputeTopicContext: Codable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let topicType: String
}

public struct ReviewDisputeReviewContext: Codable, Sendable {
    public let post: ReviewDisputePostContext
    @RequiredNullable
    public var topic: ReviewDisputeTopicContext?
    public let rating: Int
}

public struct ReviewDisputeStaffContext: Codable, Sendable {
    public let disputant: ReviewDisputeActorSummary
    public let review: ReviewDisputeReviewContext
}

public struct ReviewDisputePostContent: Codable, Sendable {
    public let text: String
    @RequiredNullable public var declaredLanguage: String?
    @RequiredNullable public var linguaRsDetectedLanguage: String?
}

public struct ReviewDispute: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String
    public let topicId: String
    public let disputantUserId: String
    public let reason: ReviewDisputeReason
    public let claimText: String
    public let status: ReviewDisputeStatus
    public let recommendedAction: ReviewDisputeAction?
    public let isOverdue: Bool?
    public let aiPublicResponse: String?
    public let aiInternalResponse: String?
    public let model: String?
    public let aiDraftedAt: Date?
    public let publicResponse: String?
    public let internalNotes: String?
    public let draftedAt: Date?
    public let editedAt: Date?
    public let editedById: String?
    public let approvedAt: Date?
    public let approvedById: String?
    public let sentAt: Date?
    public let resolvedAt: Date?
    public let resolvedById: String?
    public let resolutionAction: ReviewDisputeAction?
    public let latestLifecycleChangeId: String?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable public var postContent: ReviewDisputePostContent?
    public let staffContext: ReviewDisputeStaffContext?
}

public struct ReviewDisputeListResponse: Codable, Sendable {
    public let disputes: [ReviewDispute]
    public let pageInfo: Page<ReviewDispute>.PageInfo
}

public struct ReviewDisputeResponse: Codable, Sendable {
    public let dispute: ReviewDispute
}

public struct ReviewDisputeQueueResponse: Codable, Sendable {
    public let queued: Bool
    public let rerunById: String?
}
