import Foundation

public enum UserPrivacyAudience: String, Codable, CaseIterable, Sendable {
    case everyone
    case users
    case followers
    case mutualFollowers = "mutual_followers"
    case nobody
}

public enum DisplayNameSource: String, Codable, CaseIterable, Sendable {
    case username
    case facebook
    case xTwitter = "x"
    case apple
    case google
    case linkedin
    case microsoft
    case github
}

public enum ProfileLinkType: String, Codable, CaseIterable, Sendable {
    case url
    case twitter
    case facebook
    case instagram
    case github
    case linkedin
    case youtube
    case tiktok
}

public enum ApiKeyType: String, Codable, CaseIterable, Sendable {
    case rss
    case mcp
}

public struct EmailPreferences: Codable, Sendable {
    public let engagementEmailsEnabled: Bool
    public let newsDigestFrequency: String
    public let moderationEmailsEnabled: Bool
    public let communityDigestFrequency: String
    public let moderationEmailCadence: String
    public let moderationEmailDaysOfWeek: [Int]
    public let moderationEmailTimeOfDay: String
    public let moderationEmailTimezone: String?

    private enum CodingKeys: String, CodingKey {
        case engagementEmailsEnabled = "isEngagementEmailsEnabled"
        case newsDigestFrequency
        case moderationEmailsEnabled = "isModerationEmailsEnabled"
        case communityDigestFrequency
        case moderationEmailCadence
        case moderationEmailDaysOfWeek
        case moderationEmailTimeOfDay
        case moderationEmailTimezone
    }
}

public struct EmailPreferencesResponse: Codable, Sendable {
    public let emailPreferences: EmailPreferences
}

public struct AuthSession: Codable, Identifiable, Sendable {
    public let id: String
    public let deviceId: String
    public let deviceName: String?
    public let userAgent: String?
    public let ipAddress: String?
    public let createdAt: Date
    public let lastSeenAt: Date?
    public let expiresAt: Date?
    public let isCurrent: Bool
}

public struct BeginBlueskyAccountLinkResponse: Codable, Sendable {
    public let redirectUrl: String
    public let flowId: String?
}

public struct ProfileLink: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let linkType: ProfileLinkType
    public let sortOrder: Int
    public let urlId: String?
    public let url: String?
    public let handle: String?
    public let name: String?
    public let imageId: String?
    public let imagePlacement: ImagePlacement?
    public let createdAt: Date
    public let updatedAt: Date
}

public struct ApiKey: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let prefix: String
    public let type: ApiKeyType
    public let label: String
    public let permissions: [String]
    public let createdAt: Date
    @RequiredNullable public var lastUsedAt: Date?
    @RequiredNullable public var revokedAt: Date?
    @RequiredNullable public var expiresAt: Date?
    @RequiredNullable public var expiryReminderSentAt: Date?
    @RequiredNullable public var replacedByApiKeyId: String?
    public let updatedAt: Date
}

public struct ApiKeyCreationResponse: Codable, Sendable {
    public let apiKey: ApiKey
    public let rawKey: String
}

public struct WebPushSubscription: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let endpoint: String
    public let p256dh: String
    public let auth: String
    public let expirationTimeMs: String?
    public let userAgent: String
    public let lastSuccessAt: Date?
    public let lastFailureAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
}

public enum DataRequestStatus: String, Codable, CaseIterable, Sendable {
    case pending
    case processing
    case ready
    case failed
    case expired
}

public struct UserDataRequest: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String?
    public let queuedAt: Date?
    public let processingStartedAt: Date?
    public let completedAt: Date?
    public let failedAt: Date?
    public let expiresAt: Date?
    public let createdAt: Date?
    public let updatedAt: Date?
    public let status: DataRequestStatus
    public let downloadURL: String?

    private enum CodingKeys: String, CodingKey {
        case id
        case userId
        case queuedAt
        case processingStartedAt
        case completedAt
        case failedAt
        case expiresAt
        case createdAt
        case updatedAt
        case status
        case downloadURL = "downloadUrl"
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encodeIfPresent(userId, forKey: .userId)
        try container.encodeIfPresent(queuedAt, forKey: .queuedAt)
        try container.encodeIfPresent(processingStartedAt, forKey: .processingStartedAt)
        try container.encodeIfPresent(completedAt, forKey: .completedAt)
        try container.encodeIfPresent(failedAt, forKey: .failedAt)
        try container.encode(expiresAt, forKey: .expiresAt)
        try container.encodeIfPresent(createdAt, forKey: .createdAt)
        try container.encodeIfPresent(updatedAt, forKey: .updatedAt)
        try container.encode(status, forKey: .status)
        try container.encodeIfPresent(downloadURL, forKey: .downloadURL)
    }
}
