import Foundation

public enum CrmMessageDirection: String, Codable, CaseIterable, Sendable {
    case inbound
    case outbound
}

public enum CrmEmailProvider: String, Codable, CaseIterable, Sendable {
    case ses
    case gmailSmtp = "gmail_smtp"
}

public struct CrmMessage: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String?
    public let conversationId: String
    public let direction: CrmMessageDirection
    public let fromEmail: String
    public let toEmail: String
    public let subject: String?
    public let bodyText: String?
    public let bodyHtml: String?
    public let emailProvider: CrmEmailProvider?
    public let sesMessageId: String?
    public let sentAt: Date?
    public let deliveredAt: Date?
    public let bouncedAt: Date?
    public let receivedAt: Date?
    public let discardedAt: Date?
    public let aiPrompt: String?
    public let aiGeneratedAt: Date?
    public let sentById: String?
    public let createdAt: Date
    public let updatedAt: Date

    public init(
        id: String,
        conversationId: String,
        direction: CrmMessageDirection,
        fromEmail: String,
        toEmail: String,
        subject: String? = nil,
        bodyText: String? = nil,
        bodyHtml: String? = nil,
        emailProvider: CrmEmailProvider? = nil,
        sesMessageId: String? = nil,
        sentAt: Date? = nil,
        deliveredAt: Date? = nil,
        bouncedAt: Date? = nil,
        receivedAt: Date? = nil,
        discardedAt: Date? = nil,
        aiPrompt: String? = nil,
        aiGeneratedAt: Date? = nil,
        sentById: String? = nil,
        createdAt: Date,
        updatedAt: Date,
        entityType: String? = "crm_message"
    ) {
        self.id = id
        self.entityType = entityType
        self.conversationId = conversationId
        self.direction = direction
        self.fromEmail = fromEmail
        self.toEmail = toEmail
        self.subject = subject
        self.bodyText = bodyText
        self.bodyHtml = bodyHtml
        self.emailProvider = emailProvider
        self.sesMessageId = sesMessageId
        self.sentAt = sentAt
        self.deliveredAt = deliveredAt
        self.bouncedAt = bouncedAt
        self.receivedAt = receivedAt
        self.discardedAt = discardedAt
        self.aiPrompt = aiPrompt
        self.aiGeneratedAt = aiGeneratedAt
        self.sentById = sentById
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    enum CodingKeys: String, CodingKey {
        case id
        case entityType = "__entity_type"
        case conversationId
        case direction
        case fromEmail
        case toEmail
        case subject
        case bodyText
        case bodyHtml
        case emailProvider
        case sesMessageId
        case sentAt
        case deliveredAt
        case bouncedAt
        case receivedAt
        case discardedAt
        case aiPrompt
        case aiGeneratedAt
        case sentById
        case createdAt
        case updatedAt
    }
}

public struct CrmMessageResponse: Codable, Sendable {
    public let message: CrmMessage
}

public struct CrmNote: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String?
    public let conversationId: String
    public let contactId: String
    public let body: String
    public let createdById: String
    public let deletedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date

    public init(
        id: String,
        conversationId: String,
        contactId: String,
        body: String,
        createdById: String,
        deletedAt: Date? = nil,
        createdAt: Date,
        updatedAt: Date,
        entityType: String? = "crm_note"
    ) {
        self.id = id
        self.entityType = entityType
        self.conversationId = conversationId
        self.contactId = contactId
        self.body = body
        self.createdById = createdById
        self.deletedAt = deletedAt
        self.createdAt = createdAt
        self.updatedAt = updatedAt
    }

    enum CodingKeys: String, CodingKey {
        case id
        case entityType = "__entity_type"
        case conversationId
        case contactId
        case body
        case createdById
        case deletedAt
        case createdAt
        case updatedAt
    }
}

public struct CrmNoteResponse: Codable, Sendable {
    public let note: CrmNote
}

public struct CrmEmailDraft: Codable, Sendable {
    public let subject: String
    public let bodyHtml: String
    public let bodyText: String
}

public struct CrmEmailDraftResponse: Codable, Sendable {
    public let draft: CrmEmailDraft
}

public struct CrmImportBatch: Codable, Identifiable, Sendable {
    public let id: String
    public let importType: String
    public let totalRows: Int
    public let createdAt: Date
}

public struct CrmImportBatchResponse: Codable, Sendable {
    public let valid: Bool
    public let batch: CrmImportBatch?
    public let error: String?
    public let validation: CrmImportValidation?
}

public struct CrmImportValidation: Codable, Sendable {
    public let valid: Bool
    public let rows: [CrmImportValidationRow]
}

public struct CrmImportValidationRow: Codable, Sendable {
    public let rowIndex: Int
    public let valid: Bool
    public let errors: [String]
}
