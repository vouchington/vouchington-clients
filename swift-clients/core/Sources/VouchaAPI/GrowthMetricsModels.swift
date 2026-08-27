import Foundation
import VouchaModels

public enum GrowthRange: String, Codable, CaseIterable, Sendable {
    case today
    case sevenDays = "7d"
    case thirtyDays = "30d"
    case ninetyDays = "90d"
    case all
}

public struct GrowthMetrics: Codable, Sendable {
    public let range: GrowthRange
    public let periodStart: Date
    public let periodEnd: Date
    public let userGrowth: UserGrowthMetrics
    public let contentProduction: ContentProductionMetrics
    public let engagement: EngagementMetrics
    public let networkEffects: NetworkEffectsMetrics
    public let revenue: RevenueMetrics
    public let infrastructure: InfrastructureMetrics
}

public struct DailyDataPoint: Codable, Hashable, Sendable {
    public let date: String
    public let count: Int
}

public struct UserGrowthMetrics: Codable, Sendable {
    public let totalUsers: Int
    public let newUsers: Int
    public let dau: Int
    public let mau: Int
    public let dauMauRatio: Double
    public let signupsOverTime: [DailyDataPoint]
}

public struct ContentByType: Codable, Sendable {
    public let review: Int
    public let dataPoint: Int
    public let discussion: Int
    public let comment: Int
    public let story: Int
}

public struct ContentProductionMetrics: Codable, Sendable {
    public let totalPosts: Int
    public let postsByType: ContentByType
    public let contributionsPerActiveUser: Double
    public let clearanceApprovalRate: Double
    public let contentOverTime: [DailyDataPoint]
}

public struct EngagementMetrics: Codable, Sendable {
    public let votesCast: Int
    public let commentsCreated: Int
    public let followsCreated: Int
    public let avgFollowsPerUser: Double
    public let votesOverTime: [DailyDataPoint]
    public let commentsOverTime: [DailyDataPoint]
    public let followsOverTime: [DailyDataPoint]
}

public struct NetworkEffectsMetrics: Codable, Sendable {
    public let referralCoefficient: Double
    public let topicCoverageRate: Double
    public let landingPageVisits: Int
    public let newSignups: Int
    public let signupVisitRatio: Double
}

public typealias MembershipsByTier = [String: Int]

public struct RevenueMetrics: Codable, Sendable {
    public let activeMemberships: Int
    public let membershipsByTier: MembershipsByTier
    public let mrrByCurrency: [ScaledMoneyAggregate]
    public let upgrades: Int
    public let downgrades: Int
    public let cancellations: Int
    public let churnRate: Double
}

public struct InfrastructureMetrics: Codable, Sendable {
    public let crawlerSuccessRate: Double?
    public let queueThroughput: Int?
    public let cacheHitRate: Double?
    public let aiTokenUsage: Int?
}
