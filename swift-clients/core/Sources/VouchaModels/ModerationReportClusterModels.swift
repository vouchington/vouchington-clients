import Foundation

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
    public let reportCountCapped: Bool
    public let summaryCapped: Bool
    public let reporterCount: Int
    public let reasonBreakdown: [ModerationReportReasonBreakdown]
    public let firstReportedAt: Date
    public let lastReportedAt: Date
    public let targetLabel: String
    public let targetPath: String?
    public let adminActionPath: String?
    public let targetUserId: String?
    public let targetAvailable: Bool?
    @RequiredNullable
    public var targetContent: AuthoredContentText?
    public let targetIsRestricted: Bool
    public let indicators: ModerationReportIndicators
    public let reports: [StaffModerationReport]
}

public struct StaffModerationReportDuplicateCluster: Codable, Identifiable, Sendable {
    public let id: String
    public let signal: String
    public let postCount: Int
    public let reportCount: Int
    public let reportCountCapped: Bool
    public let summaryCapped: Bool
    public let reasonBreakdown: [ModerationReportReasonBreakdown]
    public let firstReportedAt: Date
    public let lastReportedAt: Date
    public let clusters: [StaffModerationReportEntityCluster]

    public init(
        id: String,
        signal: String,
        postCount: Int,
        reportCount: Int,
        reportCountCapped: Bool = false,
        summaryCapped: Bool = false,
        reasonBreakdown: [ModerationReportReasonBreakdown],
        firstReportedAt: Date,
        lastReportedAt: Date,
        clusters: [StaffModerationReportEntityCluster]
    ) {
        self.id = id
        self.signal = signal
        self.postCount = postCount
        self.reportCount = reportCount
        self.reportCountCapped = reportCountCapped
        self.summaryCapped = summaryCapped
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
