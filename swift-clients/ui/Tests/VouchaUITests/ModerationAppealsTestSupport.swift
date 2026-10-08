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

    static func envelope(_ appeal: String) -> Data {
        Data("{\"appeal\":\(appeal)}".utf8)
    }

    static let queued = Data(#"{"queued":true,"rerun_by_id":"admin-1"}"#.utf8)

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
