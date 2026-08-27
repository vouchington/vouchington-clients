import Foundation

public enum SupportThreadStatus: String, Codable, Sendable {
    case open
    case assigned
    case resolved
    case closed
}

public enum StaffSupportThreadStatusFilter: String, Codable, Sendable {
    case open
    case assigned
    case resolved
}

public struct SupportThread: Codable, Identifiable, Sendable {
    public var id: String
    public var supportContactId: String
    public var subject: String
    public var conversationId: String?
    public var createdAt: Date
    public var updatedAt: Date
    public var assignedAt: Date?
    public var assignedToId: String?
    public var resolvedAt: Date?
    public var resolvedById: String?
    public var status: SupportThreadStatus
    public var contactUserId: String?

    enum CodingKeys: String, CodingKey {
        case id
        case supportContactId
        case subject
        case conversationId
        case createdAt
        case updatedAt
        case assignedAt
        case assignedToId
        case resolvedAt
        case resolvedById
        case status
        case contactUserId
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        supportContactId = try container.decode(String.self, forKey: .supportContactId)
        subject = try container.decode(String.self, forKey: .subject)
        conversationId = try container.decodeIfPresent(String.self, forKey: .conversationId)
        createdAt = try container.decode(Date.self, forKey: .createdAt)
        updatedAt = try container.decode(Date.self, forKey: .updatedAt)
        assignedAt = try container.decodeIfPresent(Date.self, forKey: .assignedAt)
        assignedToId = try container.decodeIfPresent(String.self, forKey: .assignedToId)
        resolvedAt = try container.decodeIfPresent(Date.self, forKey: .resolvedAt)
        resolvedById = try container.decodeIfPresent(String.self, forKey: .resolvedById)
        status = try container.decode(SupportThreadStatus.self, forKey: .status)
        contactUserId = try container.decodeIfPresent(String.self, forKey: .contactUserId)
    }

}

public struct SupportThreadResponse: Codable, Sendable {
    public let thread: SupportThread
}

public struct CreateSupportThreadResponse: Codable, Sendable {
    public let thread: SupportThread
    public let message: SupportMessage?
}

public struct SupportThreadListResponse: Codable, Sendable {
    public let results: [SupportThread]
    public let pageInfo: Page<SupportThread>.PageInfo
}

public struct SupportThreadDetailResponse: Decodable, Sendable {
    public let thread: SupportThread
    public let messages: [SupportMessage]
    public let pageInfo: Page<SupportMessage>.PageInfo
}

public enum SupportMessageDirection: String, Codable, Sendable {
    case inbound
    case outbound
}

public struct SupportMessage: Codable, Identifiable, Sendable {
    public let id: String
    public let supportThreadId: String
    public let direction: SupportMessageDirection
    public let bodyText: String
    public let bodyHtml: String
    public let createdAt: Date
    public let createdById: String?
    public let updatedAt: Date
    public let emailMessageId: String?
    public let emailSubject: String?
    public let emailFrom: String?
    public let emailTo: String?
    public let draftedAt: Date?
    public let editedAt: Date?
    public let editedById: String?
    public let approvedAt: Date?
    public let approvedById: String?
    public let sentAt: Date?

    enum CodingKeys: String, CodingKey {
        case id, supportThreadId, direction, bodyText, bodyHtml, createdAt, createdById, updatedAt
        case emailMessageId, emailSubject, emailFrom, emailTo, draftedAt, editedAt, editedById
        case approvedAt, approvedById, sentAt
    }

}

public struct SupportMessageListResponse: Codable, Sendable {
    public let results: [SupportMessage]
    public let pageInfo: Page<SupportMessage>.PageInfo
}

public struct SupportMessageResponse: Codable, Sendable {
    public let message: SupportMessage
}

public struct SupportDraftQueuedResponse: Codable, Sendable {
    public let queued: Bool
}

public struct SupportContact: Codable, Identifiable, Sendable {
    public let id: String
    public let emailAddress: String
    public let name: String
    public let userId: String?
    public let notes: String
    public let createdAt: Date
    public let updatedAt: Date

    enum CodingKeys: String, CodingKey {
        case id, emailAddress, name, userId, notes, createdAt, updatedAt
    }

}

public struct SupportContactListResponse: Codable, Sendable {
    public let results: [SupportContact]
    public let pageInfo: Page<SupportContact>.PageInfo
}

public struct SupportContactDetailResponse: Codable, Sendable {
    public let contact: SupportContact
    public let threads: [SupportThread]
    public let threadPageInfo: Page<SupportThread>.PageInfo
}

public struct SupportContactResponse: Codable, Sendable {
    public let contact: SupportContact
}
