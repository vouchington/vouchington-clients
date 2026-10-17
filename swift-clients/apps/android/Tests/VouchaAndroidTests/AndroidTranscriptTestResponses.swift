import Foundation

extension AndroidTranscriptTestFactory {
    static func conversationResponse(id: String, title: String) -> Data {
        jsonData([
            "conversation": [
                "id": id,
                "title": title,
                "created_at": "2026-09-29T00:00:00.000Z",
                "updated_at": "2026-09-29T00:00:00.000Z"
            ]
        ])
    }

    static func conversationListResponse(
        id: String,
        title: String,
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        let cursor: Any = if let endCursor {
            endCursor
        } else {
            NSNull()
        }
        jsonData([
            "results": [[
                "id": id,
                "title": title,
                "created_at": "2026-09-29T00:00:00.000Z",
                "updated_at": "2026-09-29T00:00:00.000Z"
            ]],
            "page_info": ["has_next_page": hasNextPage, "end_cursor": cursor]
        ])
    }

    static func duplicateConversationListPage(firstID: String, secondID: String) -> Data {
        let first = [
            "id": firstID,
            "title": "First",
            "created_at": "2026-09-29T00:00:00.000Z",
            "updated_at": "2026-09-29T00:00:00.000Z"
        ]
        let second = [
            "id": secondID,
            "title": "Second",
            "created_at": "2026-09-29T00:00:00.000Z",
            "updated_at": "2026-09-29T00:00:00.000Z"
        ]
        return jsonData([
            "results": [first, second],
            "page_info": ["has_next_page": false, "end_cursor": NSNull()]
        ])
    }

    static func twoConversationListResponse(firstID: String, secondID: String) -> Data {
        let first = [
            "id": firstID,
            "title": "First",
            "created_at": "2026-09-29T00:00:00.000Z",
            "updated_at": "2026-09-29T00:00:00.000Z"
        ]
        let second = [
            "id": secondID,
            "title": "Second",
            "created_at": "2026-09-29T00:00:00.000Z",
            "updated_at": "2026-09-29T00:00:00.000Z"
        ]
        return jsonData([
            "results": [first, second],
            "page_info": ["has_next_page": false, "end_cursor": NSNull()]
        ])
    }

    static func messagePageResponse(id: String, conversationID: String, content: String) -> Data {
        jsonData([
            "results": [[
                "id": id,
                "conversation_id": conversationID,
                "created_at": "2026-09-29T00:00:00.000Z",
                "updated_at": "2026-09-29T00:00:00.000Z",
                "content": ["role": "assistant", "content": content],
                "completion": ["status": "completed"]
            ]],
            "page_info": ["has_next_page": false, "end_cursor": NSNull()]
        ])
    }

    static func emptyMessagePageResponse() -> Data {
        jsonData(["results": [], "page_info": ["has_next_page": false, "end_cursor": NSNull()]])
    }

    static func generatedChatResponse(
        conversationID: String,
        userText: String,
        assistantText: String,
        userMessageID: String = generatedUserMessageID,
        assistantMessageID: String = generatedAssistantMessageID
    ) -> Data {
        let userMessage = chatMessage(
            id: userMessageID,
            conversationID: conversationID,
            role: "user",
            content: userText
        )
        let assistantMessage = chatMessage(
            id: assistantMessageID,
            conversationID: conversationID,
            role: "assistant",
            content: assistantText
        )
        return jsonData([
            "user_message": userMessage,
            "assistant_message": assistantMessage,
            "turn": [
                "user_message_id": userMessageID,
                "assistant_message_id": assistantMessageID
            ]
        ])
    }

    private static func chatMessage(
        id: String,
        conversationID: String,
        role: String,
        content: String
    ) -> [String: Any] {
        [
            "id": id,
            "conversation_id": conversationID,
            "created_at": "2026-09-29T00:00:00.000Z",
            "updated_at": "2026-09-29T00:00:00.000Z",
            "content": ["role": role, "content": content],
            "completion": ["status": "completed"]
        ]
    }

    private static func jsonData(_ object: [String: Any]) -> Data {
        do {
            return try JSONSerialization.data(withJSONObject: object, options: [.sortedKeys])
        } catch {
            preconditionFailure("Invalid test response: \(error)")
        }
    }
}
