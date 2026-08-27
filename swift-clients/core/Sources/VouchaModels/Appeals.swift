import Foundation

public enum ModerationAppealStatus: String, CaseIterable, Codable, Sendable {
    case pending, dismissed, resolved
}

public enum ModerationAppealAction: String, Codable, Sendable {
    case accept, reduce, deny
}

public enum ModerationAppealTargetType: String, Codable, Sendable {
    case warning, ban, removal, suspension
}

public enum ModerationAppealPostRemovalKind: String, Codable, Sendable {
    case platform, community
}

public enum ModerationAppealReason: String, CaseIterable, Codable, Sendable {
    case incorrectFacts = "incorrect_facts"
    case wrongRule = "wrong_rule"
    case contextMissing = "context_missing"
    case disproportionate
    case other
}

public struct ModerationAppealSubmissionRequest: Sendable {
    public static let maximumAppealReasonUtf16Count = 4_000

    public let targetType: ModerationAppealTargetType
    public let targetId: String?
    public let reason: ModerationAppealReason
    public let details: String
    public let postRemovalKind: ModerationAppealPostRemovalKind?
    public let turnstileToken: String?

    public var encodedAppealReason: String {
        "[\(reason.rawValue)] \(details)"
    }

    public init(
        targetType: ModerationAppealTargetType,
        targetId: String? = nil,
        reason: ModerationAppealReason,
        details: String,
        postRemovalKind: ModerationAppealPostRemovalKind? = nil,
        turnstileToken: String? = nil
    ) {
        self.targetType = targetType
        self.targetId = targetId
        self.reason = reason
        self.details = details
        self.postRemovalKind = postRemovalKind
        self.turnstileToken = turnstileToken
    }
}

public struct ModerationAppeal: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String?
    public let appellantId: String?
    public let userWarningId: String?
    @RequiredNullable
    public var userSuspensionId: String?
    public let communityBanId: String?
    public let postId: String?
    public let communityId: String?
    public let postRemovalKind: ModerationAppealPostRemovalKind?
    public let appealReason: String?
    public let status: ModerationAppealStatus
    public let recommendedAction: ModerationAppealAction?
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
    public let resolutionAction: ModerationAppealAction?
    public let latestLifecycleChangeId: String?
    public let createdAt: Date
    public let updatedAt: Date
    public let isOverdue: Bool?
    public let targetContext: ModerationAppealTargetContext?
    public let staffContext: ModerationAppealStaffContext?
}

public struct ModerationAppealEnvelope: Codable, Sendable {
    public let appeal: ModerationAppeal
}

public struct ModerationAppealListResponse: Codable, Sendable {
    public let appeals: [ModerationAppeal]
    public let pageInfo: Page<ModerationAppeal>.PageInfo
}

public struct ModerationAppealSubmissionResponse: Codable, Sendable {
    public let appeal: ModerationAppeal
    public let isDuplicate: Bool
}

public struct ModerationAppealQueueResponse: Codable, Sendable {
    public let queued: Bool
    public let rerunById: String?
}

public enum ModerationDisputeStatus: String, Codable, Sendable {
    case pending, dismissed, resolved
}

public struct ModerationDispute: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String?
    public let status: ModerationDisputeStatus
    public let reason: String?
    public let claimText: String?
    public let topicId: String?
    public let disputantUserId: String?
    public let recommendedAction: String?
    public let isOverdue: Bool?
    public let createdAt: Date?
    public let updatedAt: Date?
    @RequiredNullable
    public var aiDraftedAt: Date?
    @RequiredNullable
    public var aiInternalResponse: String?
    @RequiredNullable
    public var aiPublicResponse: String?
    @RequiredNullable
    public var approvedAt: Date?
    @RequiredNullable
    public var approvedById: String?
    @RequiredNullable
    public var draftedAt: Date?
    @RequiredNullable
    public var editedAt: Date?
    @RequiredNullable
    public var editedById: String?
    @RequiredNullable
    public var internalNotes: String?
    @RequiredNullable
    public var latestLifecycleChangeId: String?
    @RequiredNullable
    public var model: String?
    @RequiredNullable
    public var publicResponse: String?
    @RequiredNullable
    public var resolutionAction: String?
    @RequiredNullable
    public var resolvedAt: Date?
    @RequiredNullable
    public var resolvedById: String?
    @RequiredNullable
    public var sentAt: Date?
    public let staffContext: ReviewDisputeStaffContext?
}

public struct ModerationDisputeListResponse: Codable, Sendable {
    public let disputes: [ModerationDispute]
    public let pageInfo: Page<ModerationDispute>.PageInfo
}
