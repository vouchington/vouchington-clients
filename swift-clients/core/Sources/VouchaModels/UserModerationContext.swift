import Foundation

/// Dedicated moderation-context detail. Private signals are null for non-staff callers.
public struct UserModerationContext: Codable, Sendable {
    public let accountAgeMs: Double
    @RequiredNullable
    public var trustTier: Int?
    @RequiredNullable
    public var activeSuspension: UserModerationSuspension?
    @RequiredNullable
    public var contentRemovalCount: Int?
    @RequiredNullable
    public var contentRemovalCountCapped: Bool?
    @RequiredNullable
    public var communityRemovalCount: Int?
}

public struct UserModerationSuspension: Codable, Sendable {
    public let suspendedAt: Date
    @RequiredNullable
    public var suspendedReason: String?
}

/// Notes are access-filtered by the server for the requesting moderator.
public struct UserModNote: Codable, Sendable {
    public let id: String
    public let createdAt: Date
    public let targetUserId: String
    public let authorUserId: String
    @RequiredNullable
    public var authorUsername: String?
    @RequiredNullable
    public var authorDisplayName: String?
    @RequiredNullable
    public var communityId: String?
    @RequiredNullable
    public var communityName: String?
    public let body: String
    @RequiredNullable
    public var deletedAt: Date?
}

public struct UserModerationContextPageInfo: Codable, Sendable {
    public let hasNextPage: Bool
}

public struct UserModerationContextResponse: Codable, Sendable {
    public let context: UserModerationContext
    public let notes: [UserModNote]
    public let pageInfo: UserModerationContextPageInfo
}
