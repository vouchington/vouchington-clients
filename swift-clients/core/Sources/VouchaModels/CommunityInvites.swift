import Foundation

public struct CommunityInvite: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let communityId: String
    @RequiredNullable
    public var invitedUserId: String?
    @RequiredNullable
    public var invitedEmail: String?
    public let invitedById: String
    @RequiredNullable
    public var acceptedAt: Date?
    @RequiredNullable
    public var acceptedById: String?
    @RequiredNullable
    public var declinedAt: Date?
    @RequiredNullable
    public var revokedAt: Date?
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, communityId, invitedUserId, invitedEmail, invitedById
        case acceptedAt, acceptedById, declinedAt, revokedAt, createdAt
    }
}

public struct CreatedCommunityInvite: Codable, Sendable {
    public let entityType: String?
    public let id: String
    public let communityId: String
    public let code: String
    @RequiredNullable
    public var invitedUserId: String?
    @RequiredNullable
    public var invitedEmail: String?
    public let invitedById: String
    @RequiredNullable
    public var acceptedAt: Date?
    @RequiredNullable
    public var acceptedById: String?
    @RequiredNullable
    public var declinedAt: Date?
    @RequiredNullable
    public var revokedAt: Date?
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, communityId, code, invitedUserId, invitedEmail, invitedById
        case acceptedAt, acceptedById, declinedAt, revokedAt, createdAt
    }
}
