import Foundation

public struct EngineeringQueueStats: Codable, Sendable, Hashable {
    public let name: String
    public let waiting: Int
    public let active: Int
    public let completed: Int
    public let failed: Int
    public let delayed: Int
    public let paused: Bool
}

public struct EngineeringQueueStatsResponse: Codable, Sendable {
    public let queues: [EngineeringQueueStats]
    public let total: Int
}

public struct EngineeringAggregatedQueueStats: Codable, Sendable, Hashable {
    public let totalWaiting: Int
    public let totalActive: Int
    public let totalCompleted: Int
    public let totalFailed: Int
    public let totalDelayed: Int
    public let queueCount: Int

    public func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        try container.encode([
            "queueCount": queueCount,
            "totalActive": totalActive,
            "totalCompleted": totalCompleted,
            "totalFailed": totalFailed,
            "totalDelayed": totalDelayed,
            "totalWaiting": totalWaiting
        ])
    }
}

public struct EngineeringQueueStatsSummaryResponse: Codable, Sendable {
    public let stats: EngineeringAggregatedQueueStats
}

public struct EngineeringScheduledJob: Codable, Identifiable, Sendable, Hashable {
    public let id: String
    public let queueName: String
    public let jobName: String
    public let schedule: String
    public let description: String
}

public struct EngineeringScheduledJobsResponse: Codable, Sendable {
    public let jobs: [EngineeringScheduledJob]
}

public struct EngineeringBackfill: Codable, Identifiable, Sendable, Hashable {
    public let id: String
    public let queueName: String
    public let jobName: String
    public let description: String
    public let sourceTable: String
}

public struct EngineeringBackfillsResponse: Codable, Sendable {
    public let backfills: [EngineeringBackfill]
}

public struct EngineeringSuccessResponse: Codable, Sendable {
    public let success: Bool
}
