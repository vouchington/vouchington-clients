import Foundation

enum ReviewDisputesTestSupport {
    static func dispute(
        id: String = "dispute-1",
        status: String = "pending",
        publicResponse: String? = nil,
        aiPublicResponse: String? = "AI response",
        aiDraftedAt: String? = "2026-07-01T10:00:00Z",
        approvedAt: String? = nil,
        sentAt: String? = nil,
        resolvedAt: String? = nil,
        resolutionAction: String? = nil,
        lifecycleChangeId: String? = "change-1"
    ) -> String {
        """
        {
          "id":"\(id)","post_id":"post-1","topic_id":"topic-1",
          "disputant_user_id":"user-1","reason":"factually_inaccurate",
          "claim_text":"The published rating uses the wrong total.",
          "status":"\(status)","recommended_action":"remove","is_overdue":true,
          "ai_public_response":\(json(aiPublicResponse)),
          "ai_internal_response":"Internal evidence","model":"native-test-model",
          "ai_drafted_at":\(json(aiDraftedAt)),"public_response":\(json(publicResponse)),
          "internal_notes":"Private staff notes","drafted_at":null,"edited_at":null,
          "edited_by_id":null,"approved_at":\(json(approvedAt)),
          "approved_by_id":\(approvedAt == nil ? "null" : "\"admin-1\""),
          "sent_at":\(json(sentAt)),"resolved_at":\(json(resolvedAt)),
          "resolved_by_id":\(resolvedAt == nil ? "null" : "\"admin-1\""),
          "resolution_action":\(json(resolutionAction)),
          "latest_lifecycle_change_id":\(json(lifecycleChangeId)),
          "created_at":"2026-07-01T09:00:00Z","updated_at":"2026-07-01T10:00:00Z",
          "staff_context":{
            "disputant":{
              "id":"user-1","username":"issuer","verified_display_name":null,
              "profile_image_id":null
            },
            "review":{
              "post":{
                "id":"post-1","title":"Quarterly review","slug":"quarterly-review",
                "markdown_preview":"The rating was calculated from old data.",
                "created_by_id":"author-1","created_at":"2026-06-30T09:00:00Z"
              },
              "topic":{
                "id":"topic-1","name":"Example Rewards","slug":"example-rewards",
                "topic_type":"rewards_program"
              },
              "rating":2
            }
          }
        }
        """
    }

    static func list(
        _ disputes: [String],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        Data("""
        {"disputes":[\(disputes.joined(separator: ","))],"page_info":{
          "has_next_page":\(hasNextPage),"start_cursor":null,"end_cursor":\(json(endCursor))
        }}
        """.utf8)
    }

    static func envelope(_ dispute: String) -> Data {
        Data("{\"dispute\":\(dispute)}".utf8)
    }

    static let queued = Data(#"{"queued":true,"rerun_by_id":"admin-1"}"#.utf8)

    private static func json(_ value: String?) -> String {
        value.map { "\"\($0)\"" } ?? "null"
    }
}
