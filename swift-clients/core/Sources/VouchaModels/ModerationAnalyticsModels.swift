public struct ModerationAnalyticsQueueVolume: Codable, Sendable {
    public let totalReports: Int
    public let pendingReports: Int
    public let reportsOverTime: [DailyCountDataPoint]
    public let clearanceActionsOverTime: [DailyTypedCountDataPoint]
    public let moderatorActionsOverTime: [DailyTypedCountDataPoint]
}

public struct ModerationAnalyticsRuleViolations: Codable, Sendable {
    public let reasons: [ModerationAnalyticsReason]
    public let reasonsOverTime: [DailyTypedCountDataPoint]
}

public struct ModerationAnalyticsReason: Codable, Sendable {
    public let reason: String
    public let count: Int
}

public struct ModerationAnalyticsAutomodPerformance: Codable, Sendable {
    public let totalActions: Int
    public let autoRemoves: Int
    public let reviewedCount: Int
    public let falsePositiveCount: Int
    public let falsePositiveRate: Double?
    public let actionsOverTime: [DailyTypedCountDataPoint]
    public let confidenceDistribution: [ModerationAnalyticsConfidenceBucket]
    public let sources: [ModerationAnalyticsSource]
}

public struct ModerationAnalyticsConfidenceBucket: Codable, Sendable {
    public let bucket: String
    public let count: Int
}

public struct ModerationAnalyticsSource: Codable, Sendable {
    public let sourceType: String
    public let count: Int
}

public struct ModerationAnalyticsModeratorWorkload: Codable, Sendable {
    public let moderators: [ModerationAnalyticsModerator]
    public let users: [String: ModerationAnalyticsUser?]
}

public struct ModerationAnalyticsModerator: Codable, Sendable {
    public let actorUserId: String
    public let total: Int
    public let counts: [String: Int]
    public let weeklyCounts: [DailyTypedCountDataPoint]
}

public struct ModerationAnalyticsUser: Codable, Sendable {
    public let username: String?
}

public struct ModerationAnalyticsAppeals: Codable, Sendable {
    public let totalClosed: Int
    public let accepted: Int
    public let reduced: Int
    public let denied: Int
    public let successRate: Double?
}

public struct ModerationAnalyticsNewUserFriction: Codable, Sendable {
    public let firstPosts: Int
    public let rejectedFirstPosts: Int
    public let rejectionRate: Double?
}
