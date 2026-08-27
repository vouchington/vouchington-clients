import Foundation

public enum CommunityModerationAnalyticsRange: String, Codable, Sendable {
    case today
    case days7 = "7d"
    case days30 = "30d"
    case days90 = "90d"
    case all
}

public struct CommunityModerationAnalyticsDailyCount: Codable, Sendable {
    public let date: String
    public let count: Int
}

public struct CommunityModDailyTypeCount: Codable, Sendable {
    public let date: String
    public let type: String
    public let count: Int
}

public struct CommunityModRuleCount: Codable, Sendable {
    public let reason: String
    public let count: Int
}

public struct CommunityModConfidenceBucket: Codable, Sendable {
    public let bucket: String
    public let count: Int
}

public struct CommunityModerationAnalyticsSourceCount: Codable, Sendable {
    public let sourceType: String
    public let count: Int
}

public struct CommunityModWorkloadRow: Codable, Sendable {
    public let actorId: String
    public let total: Int
    public let counts: [String: Int]
    public let weeklyCounts: [CommunityModDailyTypeCount]
}

public struct CommunityModerationAnalyticsScope: Codable, Sendable {
    public let type: String
    public let communityId: String?
}

public struct CommunityModerationAnalyticsQueueVolume: Codable, Sendable {
    public let totalReports: Int
    public let pendingReports: Int
    public let reportsOverTime: [CommunityModerationAnalyticsDailyCount]
    public let clearanceActionsOverTime: [CommunityModDailyTypeCount]
    public let moderatorActionsOverTime: [CommunityModDailyTypeCount]
}

public struct CommunityModRuleViolations: Codable, Sendable {
    public let reasons: [CommunityModRuleCount]
    public let reasonsOverTime: [CommunityModDailyTypeCount]
}

public struct CommunityModAutomodPerformance: Codable, Sendable {
    public let totalActions: Int
    public let autoRemoves: Int
    public let reviewedCount: Int
    public let falsePositiveCount: Int
    public let falsePositiveRate: Double?
    public let actionsOverTime: [CommunityModDailyTypeCount]
    public let confidenceDistribution: [CommunityModConfidenceBucket]
    public let sources: [CommunityModerationAnalyticsSourceCount]
}

public struct CommunityModWorkload: Codable, Sendable {
    public let moderators: [CommunityModWorkloadRow]
    public let users: [String: CommunityModeratorWorkloadUser?]
}

public struct CommunityModerationAnalyticsAppeals: Codable, Sendable {
    public let totalClosed: Int
    public let accepted: Int
    public let reduced: Int
    public let denied: Int
    public let dismissed: Int
    public let successRate: Double?
}

public struct CommunityModNewUserFriction: Codable, Sendable {
    public let firstPosts: Int
    public let rejectedFirstPosts: Int
    public let rejectionRate: Double?
}

public struct CommunityModerationAnalytics: Codable, Sendable {
    public let range: CommunityModerationAnalyticsRange
    public let periodStart: Date
    public let periodEnd: Date
    public let scope: CommunityModerationAnalyticsScope
    public let queueVolume: CommunityModerationAnalyticsQueueVolume
    public let ruleViolations: CommunityModRuleViolations
    public let automodPerformance: CommunityModAutomodPerformance
    public let moderatorWorkload: CommunityModWorkload
    public let appeals: CommunityModerationAnalyticsAppeals
    public let newUserFriction: CommunityModNewUserFriction
}
