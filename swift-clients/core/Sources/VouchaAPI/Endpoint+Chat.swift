import Foundation

private struct CreateConversationBody: Encodable {
    let title: String?
}

private struct UpdateConversationTitleBody: Encodable {
    let title: String
}

private struct SendConversationMessageBody: Encodable {
    let message: String
    let provider: String?
}

private struct ClientGeneratedChatBody: Encodable {
    let message: String
    let userMessageId: String
    let assistantMessageId: String
    let assistantContent: String
    let modelProvider: String
    let modelName: String?
}

public extension Endpoint {
    static func createConversation(title: String? = nil) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/conversations", body: CreateConversationBody(title: title))
    }

    static func myConversationTitle(conversationId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/conversations/\(pathSegment(conversationId))/title")
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

    static func chatConversationStream(conversationId: String, message: String, provider: String? = nil) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/conversations/\(pathSegment(conversationId))/chat",
            headers: ["Accept": "text/event-stream"],
            body: SendConversationMessageBody(message: message, provider: provider)
        )
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
}
