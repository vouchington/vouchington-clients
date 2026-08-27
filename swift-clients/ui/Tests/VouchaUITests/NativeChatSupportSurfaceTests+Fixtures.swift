import Foundation
@testable import VouchaFeatures
@testable import VouchaModels

extension NativeChatSupportSurfaceTests {
    static let supportThreadsListData = Data(
        #"{"results":[{"id":"thread-0","support_contact_id":"contact-1","subject":"Previous request","conversation_id":"conversation-0","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}],"page_info":{"has_next_page":false,"start_cursor":"thread-0","end_cursor":null}}"#
            .utf8
    )

    static let supportThreadsPageOneData = Data(
        #"{"results":[{"id":"thread-1","support_contact_id":"contact-1","subject":"First request","conversation_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}],"page_info":{"has_next_page":true,"start_cursor":"thread-1","end_cursor":"cursor-1"}}"#
            .utf8
    )

    static let supportThreadsPageTwoData = Data(
        #"{"results":[{"id":"thread-2","support_contact_id":"contact-1","subject":"Second request","conversation_id":null,"created_at":"2026-01-01T00:01:00Z","updated_at":"2026-01-01T00:01:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}],"page_info":{"has_next_page":true,"start_cursor":"thread-2","end_cursor":"cursor-2"}}"#
            .utf8
    )

    static let errorData = Data(#"{"message":"Request failed"}"#.utf8)

    static let chatConversationListPageOneData = Data(
        #"{"results":[{"id":"conversation-1","title":"","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}],"page_info":{"has_next_page":true,"start_cursor":"conversation-1","end_cursor":"cursor-1"}}"#
            .utf8
    )

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

    static let generatedConversationData = Data(
        #"{"conversation":{"id":"conversation-1","title":"Generated chat","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:06:00Z","updated_by_id":"user-1","deleted_at":null,"deleted_by_id":null}}"#
            .utf8
    )

    static let createdConversationData = Data(
        #"{"conversation":{"id":"conversation-3","title":"","created_at":"2026-01-01T00:07:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:07:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}}"#
            .utf8
    )

    static let createdThreadData = Data(
        #"{"thread":{"id":"thread-1","support_contact_id":"contact-1","subject":"Need help","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"}}"#
            .utf8
    )

    static let supportThreadDetailData = Data(
        #"{"thread":{"id":"thread-1","support_contact_id":"contact-1","subject":"Need help","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"},"messages":[{"id":"support-message-1","support_thread_id":"thread-1","direction":"inbound","body_text":"Initial note","body_html":"<p>Initial note</p>","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}],"page_info":{"has_next_page":false,"start_cursor":"support-message-1","end_cursor":null}}"#
            .utf8
    )

    static let supportThreadDetailPageOneData = Data(
        #"{"thread":{"id":"thread-1","support_contact_id":"contact-1","subject":"Need help","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"},"messages":[{"id":"support-message-1","support_thread_id":"thread-1","direction":"inbound","body_text":"Initial note","body_html":"<p>Initial note</p>","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}],"page_info":{"has_next_page":true,"start_cursor":"support-message-1","end_cursor":"cursor-1"}}"#
            .utf8
    )

    static let supportThreadDetailPageTwoData = Data(
        #"{"thread":{"id":"thread-1","support_contact_id":"contact-1","subject":"Need help","conversation_id":"conversation-1","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":"user-1"},"messages":[{"id":"support-message-0","support_thread_id":"thread-1","direction":"outbound","body_text":"Older note","body_html":"<p>Older note</p>","created_at":"2025-12-31T23:59:00Z","created_by_id":"user-2","updated_at":"2025-12-31T23:59:00Z","email_message_id":null,"email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,"edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,"sent_at":null}],"page_info":{"has_next_page":false,"start_cursor":"support-message-0","end_cursor":null}}"#
            .utf8
    )

    static func decode<T: Decodable>(_ type: T.Type, _ json: String) throws -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let iso8601WithFractionalSeconds = Date.ISO8601FormatStyle(includingFractionalSeconds: true)
        let iso8601 = Date.ISO8601FormatStyle(includingFractionalSeconds: false)
        decoder.dateDecodingStrategy = .custom { decoder in
            let container = try decoder.singleValueContainer()
            let string = try container.decode(String.self)
            if let date = try? Date(string, strategy: iso8601WithFractionalSeconds) {
                return date
            }
            if let date = try? Date(string, strategy: iso8601) {
                return date
            }
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Cannot parse date: \(string)")
        }
        return try decoder.decode(type, from: Data(json.utf8))
    }
}
