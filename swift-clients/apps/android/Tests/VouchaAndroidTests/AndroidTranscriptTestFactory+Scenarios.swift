import Foundation

extension AndroidTranscriptTestFactory {
    static func generatedChatResponseBuilder(
        conversationID: String,
        userText: String,
        assistantText: String
    ) -> @Sendable (URLRequest) -> Data {
        { request in
            guard let body = request.httpBody,
                  let object = try? JSONSerialization.jsonObject(with: body) as? [String: Any],
                  let userMessageID = object["userMessageId"] as? String,
                  let assistantMessageID = object["assistantMessageId"] as? String
            else { return Data() }
            return generatedChatResponse(
                conversationID: conversationID,
                userText: userText,
                assistantText: assistantText,
                userMessageID: userMessageID,
                assistantMessageID: assistantMessageID
            )
        }
    }

    static func mismatchedTurnResponseBuilder(
        conversationID: String,
        userText: String,
        assistantText: String
    ) -> @Sendable (URLRequest) -> Data {
        { request in
            guard let body = request.httpBody,
                  let object = try? JSONSerialization.jsonObject(with: body) as? [String: Any],
                  let userMessageID = object["userMessageId"] as? String,
                  let assistantMessageID = object["assistantMessageId"] as? String,
                  var response = try? generatedChatResponse(
                      conversationID: conversationID,
                      userText: userText,
                      assistantText: assistantText,
                      userMessageID: userMessageID,
                      assistantMessageID: assistantMessageID
                  ).jsonObject(),
                  var turn = response["turn"] as? [String: Any]
            else { return Data() }
            turn["user_message_id"] = "different-turn-user-id"
            response["turn"] = turn
            return (try? JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])) ?? Data()
        }
    }

    static func mismatchedMessageIDResponseBuilder(
        conversationID: String,
        userText: String,
        assistantText: String
    ) -> @Sendable (URLRequest) -> Data {
        { request in
            guard let body = request.httpBody,
                  let object = try? JSONSerialization.jsonObject(with: body) as? [String: Any],
                  let userMessageID = object["userMessageId"] as? String,
                  let assistantMessageID = object["assistantMessageId"] as? String,
                  var response = try? generatedChatResponse(
                      conversationID: conversationID,
                      userText: userText,
                      assistantText: assistantText,
                      userMessageID: userMessageID,
                      assistantMessageID: assistantMessageID
                  ).jsonObject(),
                  var userMessage = response["user_message"] as? [String: Any]
            else { return Data() }
            userMessage["id"] = "different-message-id"
            response["user_message"] = userMessage
            return (try? JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])) ?? Data()
        }
    }

    static func enqueueConversationHistoryRace(
        on recorder: AndroidTranscriptRecorder,
        firstID: String,
        secondID: String
    ) {
        let firstPath = "/api/v1/my/conversations/\(firstID)/messages"
        recorder.enqueue(
            path: "/api/v1/my/conversations",
            data: twoConversationListResponse(firstID: firstID, secondID: secondID)
        )
        recorder.enqueue(
            path: firstPath,
            data: messagePageResponse(id: "stale-first-read", conversationID: firstID, content: "Stale first read"),
            held: true
        )
        recorder.enqueue(
            path: "/api/v1/my/conversations/\(secondID)/messages",
            data: messagePageResponse(
                id: "second-conversation-message",
                conversationID: secondID,
                content: "Second conversation history"
            )
        )
        recorder.enqueue(
            path: firstPath,
            data: messagePageResponse(id: "fresh-first-read", conversationID: firstID, content: "Fresh first read")
        )
    }

}
