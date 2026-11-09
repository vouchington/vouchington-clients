import Foundation

public struct UserModerationNote: Codable, Identifiable, Sendable {
    public let id: String
    public let targetUserId: String
    public let authorUserId: String?
    @RequiredNullable
    public var authorUsername: String?
    @RequiredNullable
    public var authorDisplayName: String?
    public let communityId: String?
    @RequiredNullable
    public var communityName: String?
    public let body: String
    public let createdAt: Date
    @RequiredNullable
    public var deletedAt: Date?
}

public struct UserModerationNotesResponse: Codable, Sendable {
    public let notes: [UserModerationNote]
    public let pageInfo: Page<UserModerationNote>.PageInfo
}

public struct UserModerationNoteResponse: Codable, Sendable {
    public let note: UserModerationNote
}
