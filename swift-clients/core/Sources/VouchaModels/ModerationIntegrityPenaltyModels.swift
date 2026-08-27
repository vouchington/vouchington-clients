import Foundation

public enum IntegrityPenaltyStatus: String, Codable, CaseIterable, Sendable {
    case active, revoked
}

public struct ReportIntegrityPenalty: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let reason: String
    public let sourceFlagId: String?
    public let createdById: String?
    public let revokedAt: Date?
    public let revokedById: String?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct ReportIntegrityPenaltiesResponse: Codable, Sendable {
    public let results: [ReportIntegrityPenalty]
    public let pageInfo: Page<ReportIntegrityPenalty>.PageInfo
}

public struct ReportIntegrityPenaltyEnvelope: Codable, Sendable {
    public let penalty: ReportIntegrityPenalty
}

public struct ReportIntegrityPenaltyRevokeResponse: Codable, Sendable {
    public let penalty: ReportIntegrityPenalty
    public let penaltyId: String
    public let userId: String
}

public struct VoteIntegrityPenalty: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let penaltyMultiplier: Double
    public let reason: String
    public let sourceFlagId: String?
    public let createdById: String
    public let revokedAt: Date?
    public let revokedById: String?
    public let createdAt: Date
}

public struct VoteIntegrityPenaltyFilterScope: Codable, Equatable, Sendable {
    public let source: String
    public let sourceFlagId: String?

    public var confirmsFlagOnly: Bool {
        source == "flag"
    }
}

public struct VoteIntegrityPenaltiesResponse: Codable, Sendable {
    public let results: [VoteIntegrityPenalty]
    public let pageInfo: Page<VoteIntegrityPenalty>.PageInfo
    public let filterScope: VoteIntegrityPenaltyFilterScope?
}

public struct VoteIntegrityPenaltyEnvelope: Codable, Sendable {
    public let penalty: VoteIntegrityPenalty
}
