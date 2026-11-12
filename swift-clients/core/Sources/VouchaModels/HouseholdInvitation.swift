import Foundation

public struct HouseholdInvitationUser: Codable, Identifiable, Sendable {
    public let id: String
    @RequiredNullable
    public var username: String?
}

public struct HouseholdInvitation: Codable, Identifiable, Sendable {
    public let id: String
    public let householdId: String
    public let invitedUserId: String
    public let invitedById: String
    public let invitedUser: HouseholdInvitationUser
    public let invitedBy: HouseholdInvitationUser
    public let householdOwner: HouseholdInvitationUser
    @RequiredNullable
    public var relationship: String?
    public let createdAt: Date
    public let updatedAt: Date
    @RequiredNullable
    public var acceptedAt: Date?
    @RequiredNullable
    public var declinedAt: Date?
    @RequiredNullable
    public var revokedAt: Date?
}

public typealias HouseholdInvitationPage = Page<HouseholdInvitation>

public struct HouseholdInvitationResponse: Codable, Sendable {
    public let invitation: HouseholdInvitation
}

public struct HouseholdInvitationAcceptanceResponse: Codable, Sendable {
    public let membership: HouseholdMembership
}
