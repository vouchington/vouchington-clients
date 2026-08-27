import Foundation
@testable import VouchaModels

extension NativeChatTitleProviderTests {
    func makeConversation(
        id: String,
        title: String,
        updatedAt: String
    ) -> ChatConversation {
        ChatConversation(
            id: id,
            title: title,
            createdAt: Date(timeIntervalSince1970: 0),
            createdById: "user-1",
            updatedAt: ISO8601DateFormatter().date(from: updatedAt) ?? Date(timeIntervalSince1970: 0),
            updatedById: nil,
            deletedAt: nil,
            deletedById: nil,
            lastResponseId: nil
        )
    }

    func waitForCapturedRequestCount(_ count: Int) async {
        for _ in 0 ..< 100 {
            if CannedFeedURLProtocol.capturedBodies.count >= count {
                return
            }
            try? await Task.sleep(nanoseconds: 1_000_000)
        }
    }

    static let generatedConversationData = Data(
        """
        {
          "conversation": {
            "id": "conversation-1",
            "title": "Generated chat",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:02:00Z",
            "updated_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null
          }
        }
        """.utf8
    )

    static let manualConversationData = Data(
        """
        {
          "conversation": {
            "id": "conversation-1",
            "title": "Manual title",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:03:00Z",
            "updated_by_id": "user-1",
            "deleted_at": null,
            "deleted_by_id": null
          }
        }
        """.utf8
    )

    static let errorData = Data(#"{"message":"Request failed"}"#.utf8)

    static let clientGeneratedChatData = Data(
        """
        {
          "user_message": {
            "id": "persisted-user-1",
            "conversation_id": "conversation-1",
            "created_at": "2026-01-01T00:00:00Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:00:00Z",
            "updated_by_id": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "content": {
              "role": "user",
              "content": "How should I redeem points?"
            }
          },
          "assistant_message": {
            "id": "persisted-assistant-1",
            "conversation_id": "conversation-1",
            "created_at": "2026-01-01T00:00:01Z",
            "created_by_id": "user-1",
            "updated_at": "2026-01-01T00:00:01Z",
            "updated_by_id": null,
            "deleted_at": null,
            "deleted_by_id": null,
            "content": {
              "role": "assistant",
              "content": "Local assistant answer"
            }
          },
          "agentic_run": {
            "id": "run-1",
            "conversation_id": "conversation-1",
            "conversation_message_id": "persisted-assistant-1",
            "parent_agentic_run_id": null,
            "model_name": "apple-foundation-system",
            "model_provider": "apple_foundation",
            "input": {"message": "How should I redeem points?"},
            "output": {"response": "Local assistant answer"},
            "error": null,
            "status": "completed",
            "termination_reason": "no_tool_calls",
            "started_at": "2026-01-01T00:00:01Z",
            "completed_at": "2026-01-01T00:00:01Z",
            "failed_at": null,
            "created_at": "2026-01-01T00:00:01Z",
            "updated_at": "2026-01-01T00:00:01Z",
            "deleted_at": null
          }
        }
        """.utf8
    )
}
