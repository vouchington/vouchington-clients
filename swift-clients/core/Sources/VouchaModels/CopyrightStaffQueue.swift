import Foundation

public struct CopyrightStaffQueueResponse: Codable, Sendable {
    public let copyrightNotices: [CopyrightStaffQueueCase]
    public let pageInfo: Page<CopyrightStaffQueueCase>.PageInfo
}

public struct CopyrightStaffQueueCase: Codable, Identifiable, Sendable {
    public let id: String
    public let receivedAt: Date
    public let jurisdiction: CopyrightNoticeJurisdiction
    public let claimant: CopyrightStaffClaimant
    public let workDescription: String
    public let targets: [CopyrightStaffTarget]
    public let evidence: [CopyrightStaffEvidence]
    @RequiredNullable
    public var formReview: CopyrightStaffFormReview?
    public let restrictions: [CopyrightStaffRestriction]
    public let appeals: [CopyrightStaffAppeal]
    public let counterNotices: [CopyrightStaffSubmission]
    public let legalHolds: [CopyrightStaffLegalHold]
    public let actionIntents: [CopyrightStaffActionIntent]
    public let deliveryIntents: [CopyrightStaffDeliveryIntent]
    public let staydownMatches: [CopyrightStaffStaydownMatch]
    public let staydownMatchesHasMore: Bool
    public let emailCorrespondence: [CopyrightStaffEmailCorrespondence]
    public let reasons: [String]
    public let waitingSince: Date
    @RequiredNullable
    public var nextDeadline: CopyrightStaffDeadline?
}

public struct CopyrightStaffClaimant: Codable, Sendable {
    @RequiredNullable
    public var displayName: String?
    public let contact: String
    @RequiredNullable
    public var userId: String?
    @RequiredNullable
    public var misuse: CopyrightClaimantMisuse?
}

public struct CopyrightClaimantMisuse: Codable, Sendable {
    public let noticeCount: Int
    public let noticeRejected: Int
    public let noticeWithdrawn: Int
    public let restrictionReversedByAppeal: Int
    public let restrictionReversedByCounterNotice: Int
    @RequiredNullable
    public var oldestNoticeAt: Date?
    @RequiredNullable
    public var newestNoticeAt: Date?
    public let pageInfo: Page<CopyrightNotice>.PageInfo
}

public struct CopyrightStaffDeadline: Codable, Sendable {
    public let escalationAt: Date
    public let restorationDeadlineAt: Date
}

public struct CopyrightStaffTarget: Codable, Sendable {
    public let id: String
    public let placementKey: String
    public let placementRevision: Int
    public let imageId: String
    public let surface: String
    @RequiredNullable
    public var provenance: CopyrightStaffTargetProvenance?
    public let hostedUseUrl: String
}

public struct CopyrightStaffTargetProvenance: Codable, Sendable {
    public let setById: String
    @RequiredNullable
    public var setByAdministrator: Bool?
    @RequiredNullable
    public var uploadedById: String?
}

public struct CopyrightStaffEvidence: Codable, Sendable {
    public let id: String
    public let submissionId: String
    public let mimeType: String
    public let byteSize: Int
    public let sha256: String
}

public struct CopyrightStaffRestriction: Codable, Sendable {
    public let id: String
    public let targetId: String
    public let imposedAt: Date
    public let status: String
}

public struct CopyrightStaffAppeal: Codable, Sendable {
    public let submissionId: String
    public let reason: String
    public let targetIds: [String]
    @RequiredNullable
    public var recommendation: CopyrightStaffAppealRecommendation?
}

public struct CopyrightStaffAppealRecommendation: Codable, Sendable {
    public let id: String
    public let recommendation: String
    public let rationale: String
}

public struct CopyrightStaffSubmission: Codable, Sendable {
    public let submissionId: String
    public let receivedAt: Date
    public let targetIds: [String]
    /// Legal statements retain the producer's open-ended Record<string, unknown> boundary.
    public let statement: [String: DecodedJSONValue]
    @RequiredNullable
    public var guidance: DecodedJSONValue?
}

public struct CopyrightStaffLegalHold: Codable, Sendable {
    public let submissionId: String
    public let receivedAt: Date
    public let statement: [String: DecodedJSONValue]
    @RequiredNullable
    public var guidance: DecodedJSONValue?
    @RequiredNullable
    public var assessment: CopyrightStaffLegalHoldAssessment?
}

public struct CopyrightStaffLegalHoldAssessment: Codable, Sendable {
    public let id: String
    public let isFromOriginalClaimant: Bool
    @RequiredNullable
    public var proceedingKind: String?
    @RequiredNullable
    public var ccbClaimKind: String?
    @RequiredNullable
    public var commencedAt: Date?
    @RequiredNullable
    public var receivedByDesignatedAgentAt: Date?
    public let isSameMaterial: Bool
    public let targetIds: [String]
    public let qualifying: Bool
    public let resolved: Bool
}

public struct CopyrightStaffActionIntent: Codable, Sendable {
    public let id: String
    public let targetId: String
    public let action: String
    public let state: String
    @RequiredNullable
    public var failureMessage: String?
}

public struct CopyrightStaffDeliveryIntent: Codable, Sendable {
    public let id: String
    public let deliveryKind: String
    public let channel: String
    public let state: String
    public let deliveryAttemptCount: Int
}

public struct CopyrightStaffStaydownMatch: Codable, Sendable {
    public let id: String
    public let imageId: String
    public let registeredImageId: String
    public let targetId: String
    public let uploadedById: String
    @RequiredNullable
    public var uploadedByUsername: String?
    @RequiredNullable
    public var imagePlacement: ImagePlacement?
    public let matchKind: String
    public let hammingDistance: Int
    public let matchedAt: Date
}

public struct CopyrightStaffEmailCorrespondence: Codable, Sendable {
    @RequiredNullable
    public var submissionId: String?
    public let kind: String
    public let action: String
    public let reviewedAt: Date
}
