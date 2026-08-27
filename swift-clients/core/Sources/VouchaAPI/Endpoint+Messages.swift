import Foundation
import VouchaModels

public extension Endpoint {
    static func myMessageConversation(conversationId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/my/messages/\(pathSegment(conversationId))")
    }

    static func createMyMessageConversation(userId: String? = nil, userIds: [String]? = nil) -> Endpoint {
        struct Body: Encodable {
            let userId: String?
            let userIds: [String]?
        }
        return Endpoint(.POST, path: "/api/v1/my/messages", body: Body(userId: userId, userIds: userIds))
    }

    static func sendMyMessageConversationMessage(conversationId: String, text: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/messages/\(pathSegment(conversationId))/messages", body: ["text": text])
    }

    static func myMessageConversationParticipants(
        conversationId: String,
        after: String? = nil,
        limit: Int? = nil
    ) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/my/messages/\(pathSegment(conversationId))/participants",
            queryItems: [
                URLQueryItem(name: "after", value: after),
                URLQueryItem(name: "limit", value: limit.map(String.init))
            ].compactMap { $0.value == nil ? nil : $0 }
        )
    }

    static func addMyMessageConversationParticipant(conversationId: String, userId: String) -> Endpoint {
        struct Body: Encodable {
            let userId: String
        }
        return Endpoint(
            .POST,
            path: "/api/v1/my/messages/\(pathSegment(conversationId))/participants",
            body: Body(userId: userId)
        )
    }

    static func removeMyMessageConversationParticipant(conversationId: String, userId: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/my/messages/\(pathSegment(conversationId))/participants/\(pathSegment(userId))"
        )
    }

    static func updateMyMessageConversationParticipantAddPolicy(
        conversationId: String,
        policy: ConversationParticipantAddPolicy
    ) -> Endpoint {
        struct Body: Encodable {
            let participantAddPolicy: ConversationParticipantAddPolicy
        }
        return Endpoint(
            .PATCH,
            path: "/api/v1/my/messages/\(pathSegment(conversationId))",
            body: Body(participantAddPolicy: policy)
        )
    }

    static func myMessageUserSearch(query: String, after: String? = nil, limit: Int = 10) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/users",
            queryItems: [
                URLQueryItem(name: "q", value: query),
                URLQueryItem(name: "after", value: after),
                URLQueryItem(name: "limit", value: "\(limit)")
            ].compactMap { $0.value == nil ? nil : $0 }
        )
    }
}
