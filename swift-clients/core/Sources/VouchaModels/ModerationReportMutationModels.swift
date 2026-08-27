import Foundation

public struct ModerationReportJudgementResponse: Codable, Sendable {
    public let queued: Bool
    public let rerunById: String
}

public struct ModerationReportResolutionResponse: Codable, Sendable {
    public struct ResolvedReport: Codable, Identifiable, Sendable {
        public let id: String
        public let caseId: String
        public let createdAt: Date
        public let reviewedAt: Date
        public let reporterUserId: String
        public let entityType: String
        public let entityId: String
        public let reason: String
        public let note: String?
        public let status: ModerationReportResolution
        public let resolvedById: String
    }

    public let report: ResolvedReport
}

public struct AdminUserWarning: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String
    public let userId: String
    public let communityId: String?
    public let issuedById: String?
    public let reason: String
    public let publicMessage: String?
    public let reportId: String?
    public let revokedAt: Date?
    public let revokedById: String?
    public let createdAt: Date
}

public struct AdminUserWarningResponse: Codable, Sendable {
    public let warning: AdminUserWarning
}
