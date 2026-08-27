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
    let assistantContent: String
    let modelProvider: String
    let modelName: String?
}

private struct CreateSupportThreadBody: Encodable {
    let subject: String
    let message: String?
    let conversationId: String?
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

    static func chatConversationStream(
        conversationId: String,
        message: String,
        provider: String? = nil
    ) -> Endpoint {
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
        assistantContent: String,
        modelProvider: String,
        modelName: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/conversations/\(pathSegment(conversationId))/client-generated-chat",
            body: ClientGeneratedChatBody(
                message: message,
                assistantContent: assistantContent,
                modelProvider: modelProvider,
                modelName: modelName
            )
        )
    }

    static func mySupportThreads(after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/support-threads", queryItems: items)
    }

    static func mySupportThread(threadId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/my/support-threads/\(pathSegment(threadId))",
            queryItems: items
        )
    }

    static func createSupportThread(
        subject: String,
        message: String? = nil,
        conversationId: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/support-threads",
            body: CreateSupportThreadBody(
                subject: subject,
                message: message,
                conversationId: conversationId
            )
        )
    }
}
