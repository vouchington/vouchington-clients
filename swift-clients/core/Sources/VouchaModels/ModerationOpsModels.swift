import Foundation

public enum AdminReviewQueueClearanceStatus: String, Codable, Sendable {
    case rejected, inReview = "in_review", approved, pending
}

public struct AdminReviewQueueImage: Codable, Sendable {
    public let imageId: String
    public let orderIndex: Int
    public let caption: String
}

public struct AdminReviewQueueMediaContext: Codable, Sendable {
    public let requiresReveal: Bool
    public let images: [AdminReviewQueueImage]
}

public struct AdminReviewQueuePost: Codable, Identifiable, Sendable {
    public let id: String
    public let title: String
    public let slug: String?
    public let markdownPreview: String
    public let postType: String
    public let createdById: String?
    public let createdAt: Date
    public let rootId: String?
    public let rootPostType: String?
    public let rootSlug: String?
    public let clearanceStatus: AdminReviewQueueClearanceStatus
    public let clearanceUpdatedAt: Date?
    public let spamDetectionFlagged: Bool?
    public let spamDetectionScore: Double?
    public let spamDetectionResults: DecodedJSONValue
    public let openaiOmniModerationFlagged: Bool?
    public let openaiOmniModerationResults: DecodedJSONValue
    public let mediaContext: AdminReviewQueueMediaContext?
}

public struct AdminReviewQueueResponse: Codable, Sendable {
    public let results: [AdminReviewQueuePost]
    public let pageInfo: Page<AdminReviewQueuePost>.PageInfo
}

public struct ClearanceUpdateResponse: Codable, Sendable {
    public let clearanceStatus: AdminReviewQueueClearanceStatus
}

public enum ModerationRevealSurface: String, Codable, Sendable {
    case modQueue = "mod_queue"
    case reviewQueue = "review_queue"
    case reports
    case postPage = "post_page"
}

public struct ModerationExposureState: Codable, Sendable {
    public let count: Int
    public let threshold: Int
    public let inCooldown: Bool
    @RequiredNullable
    public var cooldownEndsAt: Date?
}

public struct ModerationExposureResponse: Codable, Sendable {
    public let exposure: ModerationExposureState
}

public struct DailyCountDataPoint: Codable, Sendable {
    public let date: String
    public let count: Int
}

public struct DailyTypedCountDataPoint: Codable, Sendable {
    public let date: String
    public let type: String
    public let count: Int
}

public struct ModerationAnalyticsScope: Codable, Sendable {
    public let type: String
    public let communityId: String?
}

public struct ModerationAnalytics: Codable, Sendable {
    public let range: String
    public let periodStart: Date
    public let periodEnd: Date
    public let scope: ModerationAnalyticsScope
    public let queueVolume: ModerationAnalyticsQueueVolume
    public let ruleViolations: ModerationAnalyticsRuleViolations
    public let automodPerformance: ModerationAnalyticsAutomodPerformance
    public let moderatorWorkload: ModerationAnalyticsModeratorWorkload
    public let appeals: ModerationAnalyticsAppeals
    public let newUserFriction: ModerationAnalyticsNewUserFriction
}
