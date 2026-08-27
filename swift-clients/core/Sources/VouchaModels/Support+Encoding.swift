import Foundation

public extension SupportThread {
    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(supportContactId, forKey: .supportContactId)
        try container.encode(subject, forKey: .subject)
        try container.encode(conversationId, forKey: .conversationId)
        try container.encode(createdAt, forKey: .createdAt)
        try container.encode(updatedAt, forKey: .updatedAt)
        try container.encode(assignedAt, forKey: .assignedAt)
        try container.encode(assignedToId, forKey: .assignedToId)
        try container.encode(resolvedAt, forKey: .resolvedAt)
        try container.encode(resolvedById, forKey: .resolvedById)
        try container.encode(status, forKey: .status)
        try container.encode(contactUserId, forKey: .contactUserId)
    }
}

public extension SupportMessage {
    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(supportThreadId, forKey: .supportThreadId)
        try container.encode(direction, forKey: .direction)
        try container.encode(bodyText, forKey: .bodyText)
        try container.encode(bodyHtml, forKey: .bodyHtml)
        try container.encode(createdAt, forKey: .createdAt)
        try container.encode(createdById, forKey: .createdById)
        try container.encode(updatedAt, forKey: .updatedAt)
        try container.encode(emailMessageId, forKey: .emailMessageId)
        try container.encode(emailSubject, forKey: .emailSubject)
        try container.encode(emailFrom, forKey: .emailFrom)
        try container.encode(emailTo, forKey: .emailTo)
        try container.encode(draftedAt, forKey: .draftedAt)
        try container.encode(editedAt, forKey: .editedAt)
        try container.encode(editedById, forKey: .editedById)
        try container.encode(approvedAt, forKey: .approvedAt)
        try container.encode(approvedById, forKey: .approvedById)
        try container.encode(sentAt, forKey: .sentAt)
    }
}

public extension SupportContact {
    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(emailAddress, forKey: .emailAddress)
        try container.encode(name, forKey: .name)
        try container.encode(userId, forKey: .userId)
        try container.encode(notes, forKey: .notes)
        try container.encode(createdAt, forKey: .createdAt)
        try container.encode(updatedAt, forKey: .updatedAt)
    }
}
