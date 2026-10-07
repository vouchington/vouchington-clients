import Foundation

public struct ChatConversation: Codable, Identifiable, Sendable {
    public var id: String
    public var title: String
    public var createdAt: Date
    public var createdById: String?
    public var updatedAt: Date
    public var updatedById: String?
    public var deletedAt: Date?
    public var deletedById: String?
    public var lastResponseId: String?
}

public struct ChatConversationResponse: Codable, Sendable {
    public let conversation: ChatConversation
}

public struct ChatConversationListResponse: Codable, Sendable {
    public let results: [ChatConversation]
    public let pageInfo: Page<ChatConversation>.PageInfo
}

public enum ChatMessageContent: Codable, Sendable {
    case string(String)
    case payload(role: String, content: String?, error: String?)

    public init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let value = try? container.decode(String.self) {
            self = .string(value)
            return
        }

        let payload = try container.decode(ChatMessageContentPayload.self)
        self = .payload(role: payload.role, content: payload.content, error: payload.error)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case let .string(value):
            try container.encode(value)
        case let .payload(role, content, error):
            try container.encode(ChatMessageContentPayload(role: role, content: content, error: error))
        }
    }

    public var role: String {
        switch self {
        case .string:
            "message"
        case let .payload(role, _, _):
            role
        }
    }

    public var content: String? {
        switch self {
        case let .string(value):
            value
        case let .payload(_, content, _):
            content
        }
    }

    public var error: String? {
        switch self {
        case .string:
            nil
        case let .payload(_, _, error):
            error
        }
    }

    public var displayText: String {
        content ?? ""
    }
}

private struct ChatMessageContentPayload: Codable {
    let role: String
    let content: String?
    let error: String?

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(role, forKey: .role)
        try container.encode(content, forKey: .content)
        try container.encode(error, forKey: .error)
    }

    private enum CodingKeys: String, CodingKey {
        case role, content, error
    }
}

public struct ChatMessageCompletion: Codable, Sendable {
    public let status: String
}

public struct ChatMessage: Codable, Identifiable, Sendable {
    public let id: String
    public let conversationId: String
    public let createdAt: Date
    public let createdById: String?
    public let updatedAt: Date
    public let updatedById: String?
    public let deletedAt: Date?
    public let deletedById: String?
    public let content: ChatMessageContent
    public let completion: ChatMessageCompletion?
}

public struct ChatMessagesResponse: Codable, Sendable {
    public let results: [ChatMessage]
    public let pageInfo: Page<ChatMessage>.PageInfo
}

public struct ClientGeneratedChatTurn: Codable, Sendable {
    public let userMessageId: String
    public let assistantMessageId: String
}

public struct ChatErrorResponse: Codable, Sendable {
    public let message: String
}

public struct ClientGeneratedChatResponse: Codable, Sendable {
    public let userMessage: ChatMessage
    public let assistantMessage: ChatMessage
    public let turn: ClientGeneratedChatTurn
}
