import Foundation

public enum CrmContactType: String, Codable, CaseIterable, Sendable {
    case influencer
    case customer
    case partner
}

public enum CrmContactSource: String, Codable, CaseIterable, Sendable {
    case csvImport = "csv_import"
    case manual
    case inboundEmail = "inbound_email"
    case referral
}

public enum CrmContactVertical: String, Codable, CaseIterable, Sendable {
    case creditCards = "credit_cards"
    case travel
    case cars
    case artificialIntelligence = "ai"
    case technology
    case finance
    case lifestyle
    case other

    public init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        let rawValue = try container.decode(String.self)
        self = Self(rawValue: rawValue) ?? .other
    }
}

public enum CrmSocialPlatform: String, Codable, CaseIterable, Sendable {
    case instagram
    case tiktok
    case youtube
    case xPlatform = "x"
    case linkedin
}

public enum CrmContactStatus: String, Codable, CaseIterable, Sendable {
    case new
    case awaitingResponse = "awaiting_response"
    case inConversation = "in_conversation"
    case converted
    case archived
    case optedOut = "opted_out"
}

public struct CrmContact: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String?
    public let name: String
    public let email: String
    public let phone: String?
    public let vertical: CrmContactVertical?
    public let contactType: CrmContactType
    public let source: CrmContactSource
    public let followerCount: Int?
    public let notes: String?
    public let metadata: [String: DecodedJSONValue]?
    public let userId: String?
    public let assignedToId: String?
    public let createdById: String
    public let contactedAt: Date?
    public let respondedAt: Date?
    public let convertedAt: Date?
    public let optedOutAt: Date?
    public let archivedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date

    public init(
        id: String,
        name: String,
        email: String,
        phone: String? = nil,
        vertical: CrmContactVertical? = nil,
        contactType: CrmContactType = .influencer,
        source: CrmContactSource = .manual,
        followerCount: Int? = nil,
        notes: String? = nil,
        metadata: [String: DecodedJSONValue]? = nil,
        userId: String? = nil,
        assignedToId: String? = nil,
        createdById: String = "00000000-0000-0000-0000-000000000000",
        contactedAt: Date? = nil,
        respondedAt: Date? = nil,
        convertedAt: Date? = nil,
        optedOutAt: Date? = nil,
        archivedAt: Date? = nil,
        createdAt: Date,
        updatedAt: Date,
        entityType: String? = "crm_contact"
    ) {
        self.id = id
        self.entityType = entityType
        self.name = name
        self.email = email
        self.phone = phone
        self.vertical = vertical
        self.contactType = contactType
        self.source = source
        self.followerCount = followerCount
        self.notes = notes
        self.metadata = metadata
        self.userId = userId
        self.assignedToId = assignedToId
        self.createdById = createdById
        self.contactedAt = contactedAt
        self.respondedAt = respondedAt
        self.convertedAt = convertedAt
        self.optedOutAt = optedOutAt
        self.archivedAt = archivedAt
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    public var status: CrmContactStatus {
        if optedOutAt != nil {
            return .optedOut
        }
        if archivedAt != nil {
            return .archived
        }
        if convertedAt != nil {
            return .converted
        }
        if respondedAt != nil {
            return .inConversation
        }
        if contactedAt != nil {
            return .awaitingResponse
        }
        return .new
    }

    enum CodingKeys: String, CodingKey {
        case id
        case entityType = "__entity_type"
        case name
        case email
        case phone
        case vertical
        case contactType
        case source
        case followerCount
        case notes
        case metadata
        case userId
        case assignedToId
        case createdById
        case contactedAt
        case respondedAt
        case convertedAt
        case optedOutAt
        case archivedAt
        case createdAt
        case updatedAt
    }
}

public struct CrmContactResponse: Codable, Sendable {
    public let contact: CrmContact
}

public struct CrmContactDetailResponse: Codable, Sendable {
    public let contact: CrmContact
    public let socialAccounts: [CrmContactSocialAccount]
}
