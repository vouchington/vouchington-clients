import Foundation

public enum NotificationEntityType: String, Codable, Sendable {
    case post
    case rssFeedItem = "rss_feed_item"
    case follow
    case referralSignup = "referral_signup"
    case referralClick = "referral_click"
    case moderationReport = "moderation_report"
    case reviewDispute = "review_dispute"
    case userWarning = "user_warning"
    case directMessage = "direct_message"
    case modmail
    case communityBan = "community_ban"
    case moderationAppeal = "moderation_appeal"
    case communityApplicationDecision = "community_application_decision"
    case communityRoleChange = "community_role_change"
    case communityOwnershipTransfer = "community_ownership_transfer"
    case communityActivityDigest = "community_activity_digest"
    /// Fallback for entity types added server-side after this client was compiled.
    case unknown

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        let raw = try container.decode(String.self)
        self = NotificationEntityType(rawValue: raw) ?? .unknown
    }
}

public struct VouchaNotification: Codable, Identifiable, Sendable {
    public let recordEntityType: String?
    public let id: String
    public let userId: String
    public let entityType: NotificationEntityType
    public let postId: String?
    public let rssFeedItemId: String?
    public let actorUserId: String?
    @RequiredNullable
    public var communityId: String?
    @RequiredNullable
    public var conversationId: String?
    @RequiredNullable
    public var moderationReportId: String?
    @RequiredNullable
    public var reviewDisputeId: String?
    @RequiredNullable
    public var userWarningId: String?
    @RequiredNullable
    public var copyrightNoticeId: String?
    @RequiredNullable
    public var actorLabel: String?
    @RequiredNullable
    public var eventKey: String?
    public let title: String
    public let body: String
    public let targetPath: String?
    @RequiredNullable
    public var targetEntity: NotificationTargetEntity?
    @RequiredNullable
    public var targetIntent: NotificationTargetIntent?
    @RequiredNullable
    public var readAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var pushedAt: Date?

    private enum CodingKeys: String, CodingKey {
        case recordEntityType = "__entityType"
        case id, userId, entityType, postId, rssFeedItemId, actorUserId, communityId
        case conversationId, moderationReportId, reviewDisputeId, userWarningId, copyrightNoticeId, actorLabel
        case eventKey, title, body, targetPath, targetEntity, targetIntent, readAt, createdAt, updatedAt, pushedAt
    }
}

public struct NotificationTargetEntity: Codable, Sendable {
    public let entityType: String
    public let id: String

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
    }
}

public struct NotificationCommunity: Codable, Sendable {
    public let id: String
    public let slug: String
    public let name: String
}

public struct NotificationResult: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    @RequiredNullable
    public var readAt: Date?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, readAt
    }
}

public enum NotificationTargetIntent: String, Codable, Sendable {
    case notificationsInbox = "notifications_inbox"
    case unknown

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        let raw = try container.decode(String.self)
        self = NotificationTargetIntent(rawValue: raw) ?? .unknown
    }
}
