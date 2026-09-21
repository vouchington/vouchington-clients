import Foundation
@testable import VouchaFeatures
@testable import VouchaModels

enum NativeChatTestFixtures {
    static let errorData = Data(#"{"message":"Request failed"}"#.utf8)
    static let chatConversationListPageTwoData = Data(
        #"{"results":[{"id":"conversation-2","title":"Second chat","created_at":"2026-01-01T00:02:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:03:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}],"page_info":{"has_next_page":false,"start_cursor":"conversation-2","end_cursor":null}}"#
            .utf8
    )
    static let chatMessagesData = Data(
        #"{"results":[{"id":"message-1","conversation_id":"conversation-1","created_at":"2026-01-01T00:04:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:04:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"assistant","content":"Hello","error":null}}],"page_info":{"has_next_page":false,"start_cursor":"message-1","end_cursor":null}}"#
            .utf8
    )
    static let renamedConversationData = Data(
        #"{"conversation":{"id":"conversation-1","title":"Renamed chat","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:05:00Z","updated_by_id":"user-1","deleted_at":null,"deleted_by_id":null}}"#
            .utf8
    )
    static let createdConversationData = Data(
        #"{"conversation":{"id":"conversation-3","title":"","created_at":"2026-01-01T00:07:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:07:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}}"#
            .utf8
    )

    static func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let fractional = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
        let standard = Date.ISO8601FormatStyle(includingFractionalSeconds: false)
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            if let date = try? Date(string, strategy: fractional) { return date }
            if let date = try? Date(string, strategy: standard) { return date }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot parse date: \(string)")
        }
        return try decoder.decode(type, from: Data(json.utf8))
    }
}
