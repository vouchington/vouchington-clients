import Foundation

public struct CommunityInvite: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let communityId: String
    public let invitedUserId: String?
    public let invitedEmail: String?
    public let invitedById: String
    public let acceptedAt: Date?
    public let acceptedById: String?
    public let declinedAt: Date?
    public let revokedAt: Date?
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
    public let invitedUserId: String?
    public let invitedEmail: String?
    public let invitedById: String
    public let acceptedAt: Date?
    public let acceptedById: String?
    public let declinedAt: Date?
    public let revokedAt: Date?
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, communityId, code, invitedUserId, invitedEmail, invitedById
        case acceptedAt, acceptedById, declinedAt, revokedAt, createdAt
    }
}
