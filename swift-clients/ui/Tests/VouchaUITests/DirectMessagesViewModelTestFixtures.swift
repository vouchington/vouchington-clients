import Foundation

extension DirectMessagesViewModelTests {
    func directMessageJSON(conversationId: String, id: String, bodyText: String) -> String {
        """
        {
          "id": "\(id)",
          "conversation_id": "\(conversationId)",
          "body_text": "\(bodyText)",
          "created_by_id": "user-1",
          "sender_username": "alice",
          "created_at": "2026-01-01T00:00:00Z",
          "updated_at": "2026-01-01T00:00:00Z",
          "deleted_at": null
        }
        """
    }

    func directMessageResponseData(conversationId: String, id: String, bodyText: String) -> Data {
        Data(
            """
            {
              "message": \(directMessageJSON(conversationId: conversationId, id: id, bodyText: bodyText))
            }
            """.utf8
        )
    }

    func conversationJSON(id: String) -> String {
        """
        {
          "id": "\(id)",
          "channel_type": null,
          "title": "Support follow-up",
          "created_at": "2026-01-01T00:00:00Z",
          "created_by_id": "user-1",
          "updated_at": "2026-01-02T00:00:00Z",
          "participant_usernames": ["alice", "bob"],
          "participant_add_policy": "all_members"
        }
        """
    }

    func jsonArray(_ values: [String]) -> String {
        "[" + values.map { "\"\($0)\"" }.joined(separator: ",") + "]"
    }

    func jsonString(_ value: String?) -> String {
        guard let value else { return "null" }
        return "\"\(value)\""
    }

    func policyResponseData() -> Data {
        Data(
            """
            {
              "participant_add_policy": "all_members"
            }
            """.utf8
        )
    }

    func addParticipantResponseData() -> Data {
        Data(
            """
            {
              "participant": {
                "id": "participant-user-2",
                "conversation_id": "conversation-1",
                "user_id": "user-2",
                "role": "member",
                "created_at": "2026-01-01T00:00:00Z",
                "removed_at": null,
                "username": "bob",
                "profile_image_id": null
              }
            }
            """.utf8
        )
    }
}
