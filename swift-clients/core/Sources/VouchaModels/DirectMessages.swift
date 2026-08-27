import Foundation

public enum ConversationParticipantAddPolicy: String, Codable, Sendable {
    case ownerOnly = "owner_only"
    case allMembers = "all_members"
}

public struct DirectConversationResponse: Codable, Sendable {
    public let conversation: DirectConversation
}

public struct DirectMessageResponse: Codable, Sendable {
    public let message: DirectMessage
}

public struct ConversationParticipantResponse: Codable, Sendable {
    public let participant: ConversationParticipant
}

public struct ConversationParticipantPolicyResponse: Codable, Sendable {
    public let participantAddPolicy: ConversationParticipantAddPolicy
}

public extension DirectMessage {
    static func optimistic(
        conversationId: String,
        bodyText: String,
        createdById: String?
    ) -> DirectMessage {
        DirectMessage(
            id: "optimistic-\(UUID().uuidString)",
            conversationId: conversationId,
            bodyText: bodyText,
            createdById: createdById,
            senderUsername: nil,
            createdAt: Date(),
            updatedAt: nil,
            deletedAt: nil
        )
    }

    init(
        id: String,
        conversationId: String,
        bodyText: String,
        createdById: String?,
        senderUsername: String?,
        createdAt: Date,
        updatedAt: Date?,
        deletedAt: Date?
    ) {
        self.id = id
        self.conversationId = conversationId
        self.bodyText = bodyText
        self.createdById = createdById
        self.senderUsername = senderUsername
        self.createdAt = createdAt
        self.updatedAt = updatedAt ?? createdAt
        self.deletedAt = deletedAt
    }
}
