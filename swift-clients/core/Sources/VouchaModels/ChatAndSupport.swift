import Foundation

public struct ChatConversation: Decodable, Identifiable, Sendable {
    public var id: String
    public var title: String
    public var createdAt: Date
    public var createdById: String
    public var updatedAt: Date
    public var updatedById: String?
    public var deletedAt: Date?
    public var deletedById: String?
    public var lastResponseId: String?
}

public struct ChatConversationResponse: Decodable, Sendable {
    public let conversation: ChatConversation
}

public struct ChatConversationListResponse: Decodable, Sendable {
    public let results: [ChatConversation]
    public let pageInfo: Page<ChatConversation>.PageInfo
}

public enum ChatMessageContent: Decodable, Sendable {
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

private struct ChatMessageContentPayload: Decodable {
    let role: String
    let content: String?
    let error: String?
}

public struct ChatMessage: Decodable, Identifiable, Sendable {
    public let id: String
    public let conversationId: String
    public let createdAt: Date
    public let createdById: String
    public let updatedAt: Date
    public let updatedById: String?
    public let deletedAt: Date?
    public let deletedById: String?
    public let content: ChatMessageContent
}

public struct ChatMessagesResponse: Decodable, Sendable {
    public let results: [ChatMessage]
    public let pageInfo: Page<ChatMessage>.PageInfo
}

public struct ClientGeneratedChatResponse: Decodable, Sendable {
    public let userMessage: ChatMessage
    public let assistantMessage: ChatMessage
    public let agenticRun: ClientGeneratedChatAgenticRun
}

public struct ClientGeneratedChatAgenticRun: Decodable, Sendable {
    public let id: String
    public let conversationId: String
    public let conversationMessageId: String
    public let parentAgenticRunId: String?
    public let modelName: String
    public let modelProvider: String
    public let input: DecodedJSONValue
    public let output: DecodedJSONValue?
    public let error: DecodedJSONValue?
    public let status: String
    public let terminationReason: String?
    public let startedAt: Date
    public let completedAt: Date?
    public let failedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    public let deletedAt: Date?
}
