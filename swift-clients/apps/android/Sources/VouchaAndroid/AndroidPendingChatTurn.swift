import Foundation
import VouchaAPI
import VouchaCore

struct AndroidPendingChatTurn: Equatable {
    let conversationID: String
    let requestBody: Data
    private let body: ClientGeneratedChatBody

    var userMessageID: String {
        body.userMessageId
    }

    var assistantMessageID: String {
        body.assistantMessageId
    }

    var userText: String {
        body.message
    }

    var assistantText: String {
        body.assistantContent
    }

    var modelProvider: String {
        body.modelProvider
    }

    var modelName: String? {
        body.modelName
    }

    init(
        conversationID: String,
        userMessageID: String,
        assistantMessageID: String,
        userText: String,
        assistantText: String,
        modelProvider: String,
        modelName: String?
    ) {
        self.conversationID = conversationID
        body = ClientGeneratedChatBody(
            message: userText,
            userMessageId: userMessageID,
            assistantMessageId: assistantMessageID,
            assistantContent: assistantText,
            modelProvider: modelProvider,
            modelName: modelName
        )
        do {
            requestBody = try APIClient.makeEncoder().encode(body)
        } catch {
            preconditionFailure("Unable to encode completed client-generated turn")
        }
    }

    init(conversationID: String, requestBody: Data) throws {
        let object = try JSONSerialization.jsonObject(with: requestBody)
        guard let values = object as? [String: Any] else { throw AndroidPendingTurnDecodeError.invalidBody }
        let required: Set = [
            "message", "user_message_id", "assistant_message_id", "assistant_content", "model_provider"
        ]
        guard required.isSubset(of: Set(values.keys)),
              Set(values.keys).isSubset(of: required.union(["model_name"]))
        else { throw AndroidPendingTurnDecodeError.invalidBody }
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let body = try decoder.decode(ClientGeneratedChatBody.self, from: requestBody)
        guard !body.message.isEmpty, !body.assistantContent.isEmpty,
              !body.userMessageId.isEmpty, !body.assistantMessageId.isEmpty,
              body.userMessageId != body.assistantMessageId,
              !body.modelProvider.isEmpty
        else { throw AndroidPendingTurnDecodeError.invalidBody }
        self.conversationID = conversationID
        self.requestBody = requestBody
        self.body = body
    }

    static func nextMessageIDs() -> (user: String, assistant: String) {
        let (user, assistant) = UuidV7.nextSequentialPair()
        return (user: user, assistant: assistant)
    }
}

private enum AndroidPendingTurnDecodeError: Error {
    case invalidBody
}
