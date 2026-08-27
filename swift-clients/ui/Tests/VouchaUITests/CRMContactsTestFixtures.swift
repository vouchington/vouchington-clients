import Foundation
import VouchaModels

enum CRMContactsTestFixtures {
    static func crmContactsPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.enumerated().map { index, id in
            contactJSON(id: id, name: "Contact \(index + 1)")
        }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"

        return Data("""
        {
          "results": [\(results)],
          "page_info": {
            "has_next_page": \(hasNextPage),
            "end_cursor": \(cursor),
            "start_cursor": null
          }
        }
        """.utf8)
    }

    static func crmMessagesPage(ids: [String], endCursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.enumerated().map { index, id in
            messageJSON(id: id, subject: "Message \(index + 1)")
        }.joined(separator: ",")
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"

        return Data("""
        {
          "results": [\(results)],
          "page_info": {
            "has_next_page": \(hasNextPage),
            "end_cursor": \(cursor),
            "start_cursor": null
          }
        }
        """.utf8)
    }

    static func crmMessagePageInfo(endCursor: String?) throws -> Page<CrmMessage>.PageInfo {
        try pageInfo(endCursor: endCursor)
    }

    static func crmNotePageInfo(endCursor: String?) throws -> Page<CrmNote>.PageInfo {
        try pageInfo(endCursor: endCursor)
    }

    static func crmContactPageInfo(endCursor: String?) throws -> Page<CrmContact>.PageInfo {
        try pageInfo(endCursor: endCursor)
    }

    private static func pageInfo<T: Decodable & Sendable>(endCursor: String?) throws -> Page<T>.PageInfo {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(
            Page<T>.PageInfo.self,
            from: Data("""
            {
              "has_next_page": true,
              "end_cursor": \(endCursor.map { "\"\($0)\"" } ?? "null"),
              "start_cursor": null
            }
            """.utf8)
        )
    }

    private static func contactJSON(id: String, name: String) -> String {
        """
        {
          "id": "\(id)",
          "__entity_type": "crm_contact",
          "name": "\(name)",
          "email": "\(id)@example.test",
          "contact_type": "influencer",
          "source": "manual",
          "created_by_id": "user-1",
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-01T00:00:00Z"
        }
        """
    }

    private static func messageJSON(id: String, subject: String) -> String {
        """
        {
          "id": "\(id)",
          "__entity_type": "crm_message",
          "conversation_id": "conversation-\(id)",
          "direction": "outbound",
          "from_email": "admin@voucha.ai",
          "to_email": "alice@example.test",
          "subject": "\(subject)",
          "body_text": "Hello",
          "email_provider": "ses",
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-01T00:00:00Z"
        }
        """
    }
}
