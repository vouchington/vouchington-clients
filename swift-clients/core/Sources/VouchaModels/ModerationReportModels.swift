import Foundation

public struct AuthoredContentText: Codable, Sendable {
    public let kind: String
    public let text: String
    @RequiredNullable public var declaredLanguage: String?
    @RequiredNullable public var linguaRsDetectedLanguage: String?
}

public enum ModerationReportStatus: String, Codable, CaseIterable, Sendable {
    case pending, reviewed, actioned, dismissed
}

public enum ModerationReportSort: String, Codable, CaseIterable, Sendable {
    case severity
    case mostReported = "most_reported"
    case createdAtAsc = "created_at_asc"
    case createdAtDesc = "created_at_desc"
}

public enum ModerationReportResolution: String, Codable, Sendable {
    case reviewed, dismissed
}

public struct ModerationReportJudgement: Codable, Sendable {
    public let recommendedAction: String
    public let publicResponse: String
    public let internalResponse: String
    public let isStale: Bool
    public let judgedReportCount: Int?
    public let currentReportCount: Int
}

public struct StaffModerationReport: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String
    public let createdAt: Date
    public let reviewedAt: Date?
    public let entityType: String
    public let entityId: String
    public let adminActionPath: String?
    public let targetLabel: String?
    public let targetPath: String?
    public let targetUserId: String?
    public let targetAvailable: Bool?
    @RequiredNullable public var targetContent: AuthoredContentText?
    public let targetIsRestricted: Bool
    public let reason: String
    public let status: ModerationReportStatus
    public let reportCount: Int
    public let reporterUserId: String
    public let reporterUsername: String?
    public let note: String?
    public let resolvedById: String?
    public let isSystemGenerated: Bool
    public let communityBanEvasion: CommunityBanEvasionContext?
    public let judgement: ModerationReportJudgement?
    @RequiredNullable
    public var postModerationContext: DecodedJSONValue?
    public let cursorCreatedAt: Date?
    public let cursorReportCount: Int?
    public let cursorSeverityRank: Int?
}

public struct MemberModerationReport: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String
    public let createdAt: Date
    public let reviewedAt: Date?
    public let entityType: String
    public let entityId: String
    public let targetLabel: String?
    public let targetPath: String?
    public let targetAvailable: Bool?
    @RequiredNullable public var targetContent: AuthoredContentText?
    public let reason: String
    public let status: ModerationReportStatus
    public let reportCount: Int
    @RequiredNullable
    public var postModerationContext: DecodedJSONValue?
}

public struct StaffFlatModerationReportsResponse: Codable, Sendable {
    public let reports: [StaffModerationReport]
    public let pageInfo: Page<StaffModerationReport>.PageInfo

    enum CodingKeys: String, CodingKey {
        case reports = "results"
        case pageInfo
    }
}

public struct MemberFlatModerationReportsResponse: Codable, Sendable {
    public let reports: [MemberModerationReport]
    public let pageInfo: Page<MemberModerationReport>.PageInfo

    enum CodingKeys: String, CodingKey {
        case reports = "results"
        case pageInfo
    }
}

public struct ModerationReportReasonBreakdown: Codable, Sendable {
    public let reason: String
    public let count: Int

    public init(reason: String, count: Int) {
        self.reason = reason
        self.count = count
    }
}

public struct ModerationReportIndicators: Codable, Sendable {
    public let contentHashDuplicate: Bool
    public let embeddingsSimilarity: Bool
    public let velocitySpike: Bool
}

public struct StaffModerationReportEntityCluster: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String
    public let entityId: String
    public let reportCount: Int
    public let reporterCount: Int
    public let reasonBreakdown: [ModerationReportReasonBreakdown]
    public let firstReportedAt: Date
    public let lastReportedAt: Date
    public let targetLabel: String?
    public let targetPath: String?
    public let adminActionPath: String?
    public let targetUserId: String?
    public let targetAvailable: Bool?
    @RequiredNullable public var targetContent: AuthoredContentText?
    public let targetIsRestricted: Bool
    public let indicators: ModerationReportIndicators
    public let reports: [StaffModerationReport]
}

public struct StaffModerationReportDuplicateCluster: Codable, Identifiable, Sendable {
    public let id: String
    public let signal: String
    public let postCount: Int
    public let reportCount: Int
    public let reasonBreakdown: [ModerationReportReasonBreakdown]
    public let firstReportedAt: Date
    public let lastReportedAt: Date
    public let clusters: [StaffModerationReportEntityCluster]

    public init(
        id: String,
        signal: String,
        postCount: Int,
        reportCount: Int,
        reasonBreakdown: [ModerationReportReasonBreakdown],
        firstReportedAt: Date,
        lastReportedAt: Date,
        clusters: [StaffModerationReportEntityCluster]
    ) {
        self.id = id
        self.signal = signal
        self.postCount = postCount
        self.reportCount = reportCount
        self.reasonBreakdown = reasonBreakdown
        self.firstReportedAt = firstReportedAt
        self.lastReportedAt = lastReportedAt
        self.clusters = clusters
    }
}

public struct StaffClusteredModerationReportsResponse: Codable, Sendable {
    public let clusterMode: String
    public let clusters: [StaffModerationReportEntityCluster]
    public let duplicateClusters: [StaffModerationReportDuplicateCluster]
    public let pageInfo: Page<StaffModerationReport>.PageInfo

    enum CodingKeys: String, CodingKey {
        case clusterMode
        case clusters = "results"
        case duplicateClusters
        case pageInfo
    }
}
