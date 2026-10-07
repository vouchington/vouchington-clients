import Foundation

public enum ReportIntegrityResolution: String, Codable, Sendable {
    case dismissed, penalized
}

public enum VoteIntegrityResolution: String, Codable, Sendable, CaseIterable {
    case dismissed, penalized, suspended
}

public struct VoteIntegrityFlag: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String?
    @RequiredNullable public var topicId: String?
    @RequiredNullable public var hostnameId: String?
    @RequiredNullable public var rssFeedItemId: String?
    @RequiredNullable public var entityRelationId: String?
    @RequiredNullable public var agentModerationId: String?
    public let flagType: String
    public let details: [String: DecodedJSONValue]
    public let resolvedAt: Date?
    public let resolvedById: String?
    public let resolution: VoteIntegrityResolution?
    public let createdAt: Date
}

public struct VoteIntegrityFlagsResponse: Codable, Sendable {
    public let results: [VoteIntegrityFlag]
    public let pageInfo: Page<VoteIntegrityFlag>.PageInfo
}

public struct VoteIntegrityFlagEnvelope: Codable, Sendable {
    public let flag: VoteIntegrityFlag
}

public struct VoteIntegrityPenaltyResponse: Codable, Sendable {
    public let penalizedUserCount: Int
    public let flag: VoteIntegrityFlag
}

public struct ReportIntegrityFlag: Codable, Identifiable, Sendable {
    public let id: String
    public let postId: String?
    public let reportedUserId: String?
    public let hostnameId: String?
    public let rssFeedItemId: String?
    public let flagType: String
    public let reporterCount: Int
    public let newAccountReporterPct: Double
    public let details: [String: DecodedJSONValue]
    public let resolvedAt: Date?
    public let resolvedById: String?
    public let resolution: ReportIntegrityResolution?
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case id, postId, reportedUserId, hostnameId, rssFeedItemId, flagType, reporterCount
        case newAccountReporterPct = "newAccountReporterPercent"
        case details, resolvedAt, resolvedById, resolution, createdAt
    }
}

public struct ReportIntegrityFlagsResponse: Codable, Sendable {
    public let results: [ReportIntegrityFlag]
    public let pageInfo: Page<ReportIntegrityFlag>.PageInfo
}

public struct ReportIntegrityFlagEnvelope: Codable, Sendable {
    public let flag: ReportIntegrityFlag
}

public struct AppliedReportIntegrityPenalty: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
}

public struct ReportIntegrityPenaltyResponse: Codable, Sendable {
    public let penalizedUserCount: Int
    public let penalties: [AppliedReportIntegrityPenalty]
    public let flag: ReportIntegrityFlag
}
