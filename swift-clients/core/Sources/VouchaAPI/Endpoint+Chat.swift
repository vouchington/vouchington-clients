import Foundation

private struct CreateConversationBody: Encodable {
    let title: String?
}

private struct UpdateConversationTitleBody: Encodable {
    let title: String
}

public struct ClientGeneratedChatBody: Codable, Sendable, Equatable {
    public let message: String
    public let userMessageId: String
    public let assistantMessageId: String
    public let assistantContent: String
    public let modelProvider: String
    public let modelName: String?

    public init(
        message: String,
        userMessageId: String,
        assistantMessageId: String,
        assistantContent: String,
        modelProvider: String,
        modelName: String?
    ) {
        self.message = message
        self.userMessageId = userMessageId
        self.assistantMessageId = assistantMessageId
        self.assistantContent = assistantContent
        self.modelProvider = modelProvider
        self.modelName = modelName
    }
}

public extension Endpoint {
    static func createConversation(title: String? = nil) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/conversations", body: CreateConversationBody(title: title))
    }

    static func renameConversation(conversationId: String, title: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/conversations/\(pathSegment(conversationId))",
            body: UpdateConversationTitleBody(title: title)
        )
    }

    static func deleteConversation(conversationId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/conversations/\(pathSegment(conversationId))")
    }

    static func clientGeneratedChat(
        conversationId: String,
        message: String,
        messageIds: (user: String, assistant: String),
        assistantContent: String,
        model: (provider: String, name: String?)
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/conversations/\(pathSegment(conversationId))/client-generated-chat",
            body: ClientGeneratedChatBody(
                message: message,
                userMessageId: messageIds.user,
                assistantMessageId: messageIds.assistant,
                assistantContent: assistantContent,
                modelProvider: model.provider,
                modelName: model.name
            )
        )
    }

    static func clientGeneratedChat(conversationId: String, serializedBody: Data) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/conversations/\(pathSegment(conversationId))/client-generated-chat",
            preEncodedJSONBody: serializedBody
        )
    }
}
