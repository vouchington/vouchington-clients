import Foundation

public enum CopyrightNoticeJurisdiction: String, Codable, Sendable {
    case usDMCA = "us_dmca"
    case euDSA = "eu_dsa"
    // swiftlint:disable:next identifier_name
    case uk
}

public struct CopyrightNoticeClaimant: Codable, Equatable, Sendable {
    public let userId: String
    public let displayName: String
}

/// One accepted copyright notice as returned by the list, public detail, or authorized
/// participant endpoints. Fields specific to detail and participant responses are optional
/// because the same read model is used for all three endpoint shapes.
public struct CopyrightNotice: Codable, Identifiable, Sendable {
    public let id: String
    public let jurisdiction: CopyrightNoticeJurisdiction
    public let receivedAt: Date
    @RequiredNullable
    public var acceptedAt: Date?
    @RequiredNullable
    public var provisionalWithholdingAt: Date?
    public let targetCount: Int
    @RequiredNullable
    public var claimant: CopyrightNoticeClaimant?
    public var targets: [CopyrightNoticeTarget]?
    public var timeline: [CopyrightNoticeTimelineEvent]?
    public var viewerRole: String?
    public var respondableTargetIds: [String]?
    public var submissions: [CopyrightNoticeSubmission]?
    public var statements: [CopyrightNoticeStatement]?
    // swiftlint:disable:next identifier_name
    public var eu: CopyrightEuParticipantCase?
}

public struct CopyrightNoticeTarget: Codable, Identifiable, Sendable {
    public let id: String
    public let surface: String
    @RequiredNullable
    public var hostedUseUrl: String?
    public let restrictionStatus: String
}

public struct CopyrightNoticeTimelineEvent: Codable, Identifiable, Sendable {
    public let id: String
    public let eventType: String
    public let createdAt: Date
}

public struct CopyrightNoticeSubmission: Codable, Identifiable, Sendable {
    public let id: String
    public let kind: String
    public let receivedAt: Date
    public let sourceKind: String
}

public struct CopyrightNoticeStatement: Codable, Identifiable, Sendable {
    public let id: String
    public let deliveryKind: String
    public let state: String
    @RequiredNullable
    public var sentAt: Date?
    public let text: String
}

public struct CopyrightEuParticipantCase: Codable, Sendable {
    @RequiredNullable public var outcome: String?
    @RequiredNullable public var decidedAt: Date?
    @RequiredNullable public var informedAt: Date?
    @RequiredNullable public var reopenedAt: Date?
    public let complaint: CopyrightEuComplaint
    public let disputeSettlements: [CopyrightEuDisputeSettlement]
    public let disputeSettlementsPageInfo: CopyrightNoticesPageInfo
}

public struct CopyrightEuComplaint: Codable, Sendable {
    public let canSubmit: Bool
    @RequiredNullable public var windowEndsAt: Date?
    @RequiredNullable public var request: CopyrightEuComplaintRequest?
    @RequiredNullable public var decision: CopyrightEuComplaintDecision?
}

public struct CopyrightEuComplaintRequest: Codable, Sendable {
    public let id: String
    public let receivedAt: Date
    public let explanation: String
    public let filedBy: String
}

public struct CopyrightEuComplaintDecision: Codable, Sendable {
    public let staffDisposition: String
    public let rationale: String
    public let decidedAt: Date
}

public struct CopyrightEuDisputeSettlement: Codable, Identifiable, Sendable {
    public let id: String
    public let bodyName: String
    public let referredAt: Date
    @RequiredNullable public var outcome: CopyrightEuDisputeOutcome?
}

public struct CopyrightEuDisputeOutcome: Codable, Sendable {
    public let result: String
    public let decidedAt: Date
    @RequiredNullable public var implementedAt: Date?
}

public struct CopyrightNoticesPageInfo: Codable, Sendable {
    public let hasNextPage: Bool
    @RequiredNullable
    public var startCursor: String?
    @RequiredNullable
    public var endCursor: String?
}

public struct CopyrightNoticesResponse: Codable, Sendable {
    public let copyrightNotices: [CopyrightNotice]
    public let pageInfo: CopyrightNoticesPageInfo
}

public struct CopyrightNoticeResponse: Codable, Sendable {
    public let copyrightNotice: CopyrightNotice
}

public struct CopyrightEuDisputeSettlementsResponse: Codable, Sendable {
    public let copyrightEuDisputeSettlements: [CopyrightEuDisputeSettlement]
    public let pageInfo: CopyrightNoticesPageInfo
}
