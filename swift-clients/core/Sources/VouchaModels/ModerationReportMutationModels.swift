import Foundation

public struct ModerationReportJudgementResponse: Codable, Sendable {
    public let runId: String
    public let queued: Bool
    public let rerunById: String
}

public enum ModerationAIRunStatus: String, Codable, Sendable {
    case active, succeeded, failed, expired

    public var isTerminal: Bool {
        self != .active
    }
}

public struct ModerationReportJudgementRunResponse: Codable, Sendable {
    public let runId: String
    public let status: ModerationAIRunStatus
    @RequiredNullable
    public var judgementId: String?
}

public struct ModerationReportDetailResponse: Codable, Sendable {
    public struct Judgement: Codable, Identifiable, Sendable {
        public let id: String
        public let internalResponse: String
        public let publicResponse: String
        public let recommendedAction: String
    }

    public struct Report: Codable, Identifiable, Sendable {
        public let id: String
        public let caseId: String
        public let createdAt: Date
        public let entityId: String
        public let entityType: String
        public let note: String?
        public let reason: String
        public let reporterUserId: String
        public let resolvedById: String?
        public let reviewedAt: Date?
        public let status: ModerationReportStatus
    }

    @RequiredNullable
    public var judgement: Judgement?
    public let report: Report
}

public struct ModerationAppealDraftRunResponse: Codable, Sendable {
    public let runId: String
    public let status: ModerationAIRunStatus
    @RequiredNullable
    public var aiDraftedAt: Date?
    @RequiredNullable
    public var latestLifecycleChangeId: String?
}

public struct ModerationReportResolutionResponse: Codable, Sendable {
    public struct ResolvedReport: Codable, Identifiable, Sendable {
        public let id: String
        public let caseId: String
        public let createdAt: Date
        public let reviewedAt: Date
        @TolerantNullable
        public var reporterDisplayName: String?
        @TolerantNullable
        public var resolverDisplayName: String?
        @TolerantNullable
        public var resolverUsername: String?
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
