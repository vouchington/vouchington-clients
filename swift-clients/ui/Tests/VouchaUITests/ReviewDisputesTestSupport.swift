import Foundation
@testable import VouchaFeatures

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
        let overrides = """
        {
          "id":"\(id)","post_id":"post-1","topic_id":"topic-1",
          "disputant_user_id":"user-1","reason":"factually_inaccurate",
          "claim_text":"The published rating uses the wrong total.",
          "status":"\(status)","recommended_action":"remove","is_overdue":true,
          "ai_public_response":\(json(aiPublicResponse)),
          "ai_internal_response":"Internal evidence","model":"native-test-model",
          "ai_draft_requested_run_id":"dispute_resolution_00000000-0000-7000-8000-000000000211",
          "ai_draft_run_id":\(json(aiDraftedAt == nil ? nil :
                  "dispute_resolution_00000000-0000-7000-8000-000000000211")),
          "ai_drafted_at":\(json(aiDraftedAt)),"public_response":\(json(publicResponse)),
          "internal_notes":"Private staff notes","drafted_at":null,"edited_at":null,
          "edited_by_id":null,"approved_at":\(json(approvedAt)),
          "approved_by_id":\(approvedAt == nil ? "null" : "\"admin-1\""),
          "sent_at":\(json(sentAt)),"resolved_at":\(json(resolvedAt)),
          "resolved_by_id":\(resolvedAt == nil ? "null" : "\"admin-1\""),
          "resolution_action":\(json(resolutionAction)),
          "latest_lifecycle_change_id":\(json(lifecycleChangeId)),
          "created_at":"2026-07-01T09:00:00Z","updated_at":"2026-07-01T10:00:00Z",
          "post_content":null,
          "staff_context":{
            "disputant":{
              "id":"user-1","username":"issuer","verified_display_name":null,
              "profile_image_id":null
            },
            "review":{
              "post":{
                "id":"post-1","title":"Quarterly review","declared_language":null,"lingua_rs_detected_language":null,"slug":"quarterly-review",
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
        do {
            let canonical = try JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.moderation.disputes.detail.default")
            ) as? [String: Any]
            guard var dispute = canonical?["dispute"] as? [String: Any],
                  let overrides = try JSONSerialization.jsonObject(with: Data(overrides.utf8)) as? [String: Any]
            else { preconditionFailure("Invalid canonical dispute fixture") }
            dispute.merge(overrides) { _, new in new }
            return try String(decoding: JSONSerialization.data(withJSONObject: dispute), as: UTF8.self)
        } catch {
            preconditionFailure("Invalid dispute fixture: \(error)")
        }
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

    static func staffList(
        _ details: [String],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        let summaryKeys: Set = [
            "id", "disputant_user_id", "user_warning_id", "user_suspension_id", "community_ban_id",
            "post_id", "community_id", "post_removal_kind", "status", "recommended_action",
            "public_response", "approved_at", "sent_at", "resolved_at", "resolution_action",
            "created_at", "updated_at", "is_overdue", "target_context", "reason", "topic_id", "post_content"
        ]
        let summaries: [String] = details.map { detail in
            guard let data = detail.data(using: .utf8),
                  let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let id = object["id"] as? String
            else { preconditionFailure("Invalid test detail") }
            CannedFeedURLProtocol.handlers["/api/v1/disputes/\(id)"] = (envelope(detail), 200)
            var summary = object.filter { summaryKeys.contains($0.key) }
            summary["has_draft"] = object["drafted_at"] is String
            summary["has_ai_draft"] = object["ai_drafted_at"] is String
            summary["public_response"] = object["sent_at"] is String
                ? object["public_response"] ?? NSNull() : NSNull()
            summary["disputant"] = [
                "id": object["disputant_user_id"] ?? "user-1", "username": "test-member",
                "verified_display_name": NSNull(), "profile_image_id": NSNull()
            ]
            summary["post_label"] = "Test review"
            summary["post_slug"] = "test-review"
            guard let encoded = try? JSONSerialization.data(withJSONObject: summary),
                  let text = String(data: encoded, encoding: .utf8)
            else { preconditionFailure("Invalid test summary") }
            return text
        }
        return list(summaries, hasNextPage: hasNextPage, endCursor: endCursor)
    }

    @MainActor
    static func openDetails(in viewModel: ReviewDisputesViewModel) async {
        for summary in viewModel.summaryPagination.items.reversed() {
            await viewModel.loadDetail(id: summary.id)
        }
    }

    static func envelope(_ dispute: String) -> Data {
        Data("{\"dispute\":\(dispute)}".utf8)
    }

    static let queued = ApiFixtureLoader.data("native.moderation.disputes.resolution-drafts.default")

    private static func json(_ value: String?) -> String {
        value.map { "\"\($0)\"" } ?? "null"
    }
}
