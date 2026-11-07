import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ModerationSummaryPrivacyContractTests: XCTestCase {
    func testStaffSummaryListAndFullDetailKeepSeparateFields() throws {
        let response = try makeVouchaDecoder().decode(
            ReviewDisputeListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.default")
        )
        let summary = try XCTUnwrap(response.disputes.first)
        XCTAssertEqual(summary.disputant.username, "native-claimant")
        XCTAssertEqual(summary.postLabel, "A disputed review")
        XCTAssertFalse(summary.hasDraft)
        XCTAssertFalse(summary.hasAiDraft)
        let detail = try makeVouchaDecoder().decode(
            ReviewDisputeResponse.self,
            from: ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        ).dispute
        XCTAssertEqual(summary.id, detail.id)
        XCTAssertFalse(detail.claimText.isEmpty)
        XCTAssertEqual(detail.staffContext?.review.rating, 1)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let fields = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(summary)) as? [String: Any])
        for key in [
            "claim_text",
            "staff_context",
            "internal_notes",
            "ai_public_response",
            "ai_draft_requested_run_id"
        ] {
            XCTAssertNil(fields[key], key)
        }
    }

    func testAppealSummaryAndDetailDecodeDistinctShapes() throws {
        let summary = try XCTUnwrap(makeVouchaDecoder().decode(
            ModerationAppealListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.default")
        ).appeals.first)
        XCTAssertEqual(summary.appellant.username, "appealing-reviewer")
        XCTAssertTrue(summary.hasAiDraft)
        XCTAssertFalse(summary.hasDraft)
        guard case let .postRemoval(target) = summary.targetContext else {
            return XCTFail("Expected public removal context")
        }
        XCTAssertEqual(target.title, "A removed review")
        let detail = try makeVouchaDecoder().decode(
            ModerationAppealEnvelope.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        ).appeal
        XCTAssertEqual(summary.id, detail.id)
        XCTAssertNotNil(detail.appealReason)
        XCTAssertEqual(detail.staffContext?.appellant.username, "appealing-reviewer")
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let fields = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(summary)) as? [String: Any])
        for key in [
            "appeal_reason",
            "staff_context",
            "internal_notes",
            "ai_public_response",
            "ai_draft_requested_run_id"
        ] {
            XCTAssertNil(fields[key], key)
        }
    }

}
