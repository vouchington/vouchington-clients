import Foundation

public struct CopyrightStaffFormReview: Codable, Sendable {
    public let intakeId: String
    public let sourceKind: String
    @RequiredNullable
    public var screening: CopyrightStaffFormScreening?
    @RequiredNullable
    public var review: CopyrightStaffFormDecision?
}

public struct CopyrightStaffFormScreening: Codable, Sendable {
    public let state: String
    @RequiredNullable
    public var recommendation: String?
    @RequiredNullable
    public var rationale: String?
    @RequiredNullable
    public var guidance: CopyrightStaffFormGuidance?
}

public struct CopyrightStaffFormDecision: Codable, Sendable {
    public let isAccepted: Bool
    public let reviewedAt: Date
    @RequiredNullable
    public var reviewedById: String?
}

public struct CopyrightStaffFormGuidance: Codable, Sendable {
    public let summary: String
    public let elements: [CopyrightStaffFormElement]
    public let riskNotes: [CopyrightStaffFormRiskNote]
    public let suggestedAction: String
}

public struct CopyrightStaffFormElement: Codable, Sendable {
    public let element: String
    public let status: String
    @RequiredNullable
    public var gap: String?
}

public struct CopyrightStaffFormRiskNote: Codable, Sendable {
    public let kind: String
    public let note: String
}

public struct CopyrightRepeatInfringerAccountsResponse: Codable, Sendable {
    public let copyrightRepeatInfringerAccounts: [CopyrightRepeatInfringerAccount]
}

public struct CopyrightRepeatInfringerAccount: Codable, Sendable {
    public let accountUserId: String
    @RequiredNullable
    public var accountUsername: String?
    public let incidentId: String
    public let isOperative: Bool
    @RequiredNullable
    public var openReviewId: String?
    public let terminationInEffect: Bool
}

public struct CopyrightAssociationTargetsResponse: Codable, Sendable {
    public let copyrightNotices: [CopyrightNoticeAssociationTarget]
    public let pageInfo: Page<CopyrightNoticeAssociationTarget>.PageInfo
}

public struct CopyrightNoticeAssociationTarget: Codable, Identifiable, Sendable {
    public let id: String
    public let jurisdiction: CopyrightNoticeJurisdiction
    public let receivedAt: Date
    public let workDescription: String
    @RequiredNullable
    public var claimantDisplayName: String?
}
