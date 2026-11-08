import Foundation
@testable import VouchaFeatures
import XCTest

enum ModerationAppealsTestSupport {
    @MainActor
    static func markPendingAppealsReconciled(in viewModel: MemberAppealsViewModel) {
        viewModel.pendingAppealPagination.reset(items: [])
        viewModel.pendingAppealPagination.restoreContinuation(endCursor: nil, hasMore: false)
    }

    static func appeal(
        id: String = "appeal-1",
        status: String = "pending",
        suspensionId: String? = nil,
        postId: String? = nil,
        postRemovalKind: String? = nil,
        publicResponse: String? = nil,
        aiPublicResponse: String? = "AI draft",
        aiDraftedAt: String? = "2026-07-01T10:00:00Z",
        internalNotes: String? = nil,
        draftedAt: String? = nil,
        editedAt: String? = nil,
        editedById: String? = nil,
        approvedAt: String? = nil,
        approvedById: String? = "admin-1",
        sentAt: String? = nil,
        resolvedAt: String? = nil,
        resolvedById: String? = nil,
        resolutionAction: String? = nil,
        lifecycleChangeId: String? = "change-1"
    ) -> String {
        """
        {
          "id":"\(id)","case_id":"case-\(id)","appellant_user_id":"user-1",
          "user_warning_id":\(suspensionId == nil && postId == nil ? quote("warning-1") : "null"),
          "user_suspension_id":\(json(suspensionId)),"community_ban_id":null,"post_id":\(json(postId)),
          "community_id":null,"post_removal_kind":\(json(postRemovalKind)),"appeal_reason":"Please reconsider.",
          "status":"\(status)","recommended_action":"reduce",
          "ai_public_response":\(json(aiPublicResponse)),"ai_internal_response":"Internal AI context",
          "model":"native-test-model","ai_drafted_at":\(json(aiDraftedAt)),
          "ai_draft_requested_run_id":"appeal_resolution_00000000-0000-7000-8000-000000000202",
          "ai_draft_run_id":\(json(aiDraftedAt == nil ? nil :
                  "appeal_resolution_00000000-0000-7000-8000-000000000202")),
          "public_response":\(json(publicResponse)),"internal_notes":\(json(internalNotes)),
          "drafted_at":\(json(draftedAt)),"edited_at":\(json(editedAt)),"edited_by_id":\(json(editedById)),
          "approved_at":\(json(approvedAt)),"approved_by_id":\(approvedAt == nil ? "null" : json(approvedById)),
          "sent_at":\(json(sentAt)),"resolved_at":\(json(resolvedAt)),
          "resolved_by_id":\(json(resolvedById)),
          "resolution_action":\(json(resolutionAction)),
          "latest_lifecycle_change_id":\(json(lifecycleChangeId)),
          "created_at":"2026-07-01T09:00:00Z","updated_at":"2026-07-01T10:00:00Z","is_overdue":true
        }
        """
    }

    static func list(
        _ appeals: [String],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        Data("""
        {"appeals":[\(appeals
            .joined(
                separator: ","
            ))],"page_info":{"has_next_page":\(hasNextPage),"start_cursor":null,"end_cursor":\(json(endCursor))}}
        """.utf8)
    }

    static func staffList(
        _ details: [String],
        hasNextPage: Bool = false,
        endCursor: String? = nil
    ) -> Data {
        let summaryKeys: Set = [
            "id", "appellant_user_id", "user_warning_id", "user_suspension_id", "community_ban_id",
            "post_id", "community_id", "post_removal_kind", "status", "recommended_action",
            "public_response", "approved_at", "sent_at", "resolved_at", "resolution_action",
            "created_at", "updated_at", "is_overdue", "target_context", "reason", "topic_id", "post_content"
        ]
        let summaries: [String] = details.map { detail in
            guard let data = detail.data(using: .utf8),
                  let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                  let id = object["id"] as? String
            else { preconditionFailure("Invalid test detail") }
            CannedFeedURLProtocol.handlers["/api/v1/appeals/\(id)"] = (envelope(detail), 200)
            var summary = object.filter { summaryKeys.contains($0.key) }
            summary["has_draft"] = object["drafted_at"] is String
            summary["has_ai_draft"] = object["ai_drafted_at"] is String
            summary["appellant"] = [
                "id": object["appellant_user_id"] ?? "user-1", "username": "test-member",
                "verified_display_name": NSNull(), "profile_image_id": NSNull()
            ]
            guard let encoded = try? JSONSerialization.data(withJSONObject: summary),
                  let text = String(data: encoded, encoding: .utf8)
            else { preconditionFailure("Invalid test summary") }
            return text
        }
        return list(summaries, hasNextPage: hasNextPage, endCursor: endCursor)
    }

    static func envelope(_ appeal: String) -> Data {
        Data("{\"appeal\":\(appeal)}".utf8)
    }

    static var queued: Data {
        ApiFixtureLoader.data("native.moderation.appeals.resolution-drafts.default")
    }

    static func waitForSuspendedCannedFeedResponse(path: String) async -> Bool {
        await waitForCannedFeedCondition(
            "Timed out waiting for suspended response at \(path)",
            condition: { CannedFeedURLProtocol.hasSuspendedResponse(path: path) }
        )
    }

    static func waitForCapturedCannedFeedRequests(path: String, minimumCount: Int) async -> Bool {
        await waitForCannedFeedCondition(
            "Timed out waiting for \(minimumCount) requests at \(path)",
            condition: { CannedFeedURLProtocol.capturedPathCount(path) >= minimumCount }
        )
    }

    static func waitForCannedFeedCondition(
        _ failureMessage: String,
        condition: @escaping () -> Bool
    ) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while ContinuousClock.now < deadline {
            if condition() {
                return true
            }
            await Task.yield()
        }
        XCTFail(failureMessage)
        return false
    }

    private static func json(_ value: String?) -> String {
        value.map(quote) ?? "null"
    }

    private static func quote(_ value: String) -> String {
        "\"\(value)\""
    }
}
