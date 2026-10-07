import Foundation

public struct Household: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let ownerId: String
    public let createdAt: Date?
    public let updatedAt: Date

    public init(id: String, ownerId: String, createdAt: Date? = nil, updatedAt: Date) {
        self.id = id
        self.ownerId = ownerId
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    private enum CodingKeys: String, CodingKey {
        case id, createdAt, updatedAt
        case ownerId = "ownerUserId"
    }
}

public struct HouseholdMembership: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let householdId: String
    public let individual: HouseholdIndividual
    public let relationship: String?
    public let updatedAt: Date

    public init(
        id: String,
        householdId: String,
        individual: HouseholdIndividual,
        relationship: String? = nil,
        updatedAt: Date
    ) {
        self.id = id
        self.householdId = householdId
        self.individual = individual
        self.relationship = relationship
        self.updatedAt = updatedAt
    }
}

public struct HouseholdIndividual: Codable, Identifiable, Hashable, Sendable {
    public let id: String
    public let userId: String?
    public let username: String?
    public let updatedAt: Date

    public init(id: String, userId: String? = nil, username: String? = nil, updatedAt: Date) {
        self.id = id
        self.userId = userId
        self.username = username
        self.updatedAt = updatedAt
    }
}

public typealias HouseholdPage = Page<Household>
public typealias HouseholdMembershipPage = Page<HouseholdMembership>

public struct HouseholdCreateResponse: Codable, Sendable {
    public let household: Household
}
