import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class ModerationSummaryReconciliationTests: XCTestCase {
    func testDisputeReconciliationRetainsContextAndProjectsOnlySentResponse() throws {
        let summary = try XCTUnwrap(makeVouchaDecoder().decode(
            ReviewDisputeListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.default")
        ).disputes.first)
        var object = try detailObject("native.moderation.disputes.detail.default", key: "dispute")
        object["public_response"] = "Unsent staff response"
        object["sent_at"] = NSNull()
        object["status"] = "resolved"
        object["resolved_at"] = "2026-07-01T13:00:00Z"
        object["ai_drafted_at"] = NSNull()
        object["ai_public_response"] = "Existing AI text without saved draft timestamp"
        let detail = try makeVouchaDecoder().decode(
            ReviewDispute.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let updated = summary.updating(from: detail)
        XCTAssertEqual(updated.id, summary.id)
        XCTAssertEqual(updated.status, detail.status)
        XCTAssertEqual(updated.resolvedAt, detail.resolvedAt)
        try assertSafeEncoding(updated)
        XCTAssertTrue(updated.hasDraft)
        XCTAssertFalse(updated.hasAiDraft)
        XCTAssertNil(updated.publicResponse)
        XCTAssertEqual(updated.disputant.username, summary.disputant.username)
        XCTAssertEqual(updated.postLabel, summary.postLabel)
        object["sent_at"] = "2026-07-01T12:00:00Z"
        let sent = try makeVouchaDecoder().decode(
            ReviewDispute.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        XCTAssertEqual(summary.updating(from: sent).publicResponse, "Unsent staff response")
        object["id"] = "different-dispute"
        let unrelated = try makeVouchaDecoder().decode(
            ReviewDispute.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        XCTAssertEqual(summary.updating(from: unrelated).hasDraft, summary.hasDraft)
    }

    func testAppealReconciliationRetainsContextAndUsesCanonicalDraftPresence() throws {
        let summary = try XCTUnwrap(makeVouchaDecoder().decode(
            ModerationAppealListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.default")
        ).appeals.first)
        var object = try detailObject("native.moderation.appeals.detail.default", key: "appeal")
        object["public_response"] = "Unsent appeal response"
        object["sent_at"] = NSNull()
        object["ai_public_response"] = NSNull()
        object["ai_drafted_at"] = "2026-07-01T10:00:00Z"
        let detail = try makeVouchaDecoder().decode(
            ModerationAppeal.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let updated = summary.updating(from: detail)
        XCTAssertEqual(updated.id, summary.id)
        XCTAssertEqual(updated.status, detail.status)
        XCTAssertEqual(updated.resolvedAt, detail.resolvedAt)
        try assertSafeEncoding(updated)
        XCTAssertTrue(updated.hasDraft)
        XCTAssertTrue(updated.hasAiDraft)
        XCTAssertNil(updated.publicResponse)
        XCTAssertEqual(updated.appellant.username, summary.appellant.username)
        XCTAssertEqual(updated.postId, summary.postId)
        XCTAssertEqual(updated.targetContext != nil, summary.targetContext != nil)
        object["sent_at"] = "2026-07-01T12:00:00Z"
        let sent = try makeVouchaDecoder().decode(
            ModerationAppeal.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        XCTAssertEqual(summary.updating(from: sent).publicResponse, "Unsent appeal response")
        object["id"] = "different-appeal"
        let unrelated = try makeVouchaDecoder().decode(
            ModerationAppeal.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        XCTAssertEqual(summary.updating(from: unrelated).hasDraft, summary.hasDraft)
    }

    private func assertSafeEncoding(_ value: some Encodable) throws {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(value)) as? [String: Any])
        for key in [
            "claim_text",
            "appeal_reason",
            "staff_context",
            "internal_notes",
            "ai_public_response",
            "ai_internal_response",
            "ai_draft_run_id",
            "ai_draft_requested_run_id"
        ] {
            XCTAssertNil(object[key], key)
        }
    }

    private func detailObject(_ fixture: String, key: String) throws -> [String: Any] {
        let envelope = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any])
        return try XCTUnwrap(envelope[key] as? [String: Any])
    }
}
