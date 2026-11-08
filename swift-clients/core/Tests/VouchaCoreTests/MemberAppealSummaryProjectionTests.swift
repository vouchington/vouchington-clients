import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class MemberAppealSummaryProjectionTests: XCTestCase {
    func testStaffPersonalListRetainsTypedSummaryAndTrackingFields() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.default")
        let response = try makeVouchaDecoder().decode(MemberAppealListResponse.self, from: data)
        let projection = try XCTUnwrap(response.appeals.first)
        guard case let .summary(summary) = projection else { return XCTFail("Expected staff summary") }
        XCTAssertEqual(projection.id, summary.id)
        XCTAssertEqual(projection.userWarningId, summary.userWarningId)
        XCTAssertEqual(projection.userSuspensionId, summary.userSuspensionId)
        XCTAssertEqual(projection.communityBanId, summary.communityBanId)
        XCTAssertEqual(projection.postId, summary.postId)
        XCTAssertEqual(projection.postRemovalKind, summary.postRemovalKind)
        XCTAssertEqual(projection.status, summary.status)
        XCTAssertEqual(projection.publicResponse, summary.publicResponse)
        XCTAssertEqual(projection.approvedAt, summary.approvedAt)
        XCTAssertEqual(projection.sentAt, summary.sentAt)
        XCTAssertEqual(projection.resolvedAt, summary.resolvedAt)
        XCTAssertEqual(projection.resolutionAction, summary.resolutionAction)
        XCTAssertEqual(projection.createdAt, summary.createdAt)
        XCTAssertNil(projection.caseId)
        XCTAssertEqual(projection.targetContext != nil, summary.targetContext != nil)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let encoded = try encoder.encode(projection)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        XCTAssertEqual(object["has_draft"] as? Bool, summary.hasDraft)
        XCTAssertNil(object["ai_draft_requested_run_id"])
        XCTAssertThrowsError(try makeVouchaDecoder().decode(ModerationAppeal.self, from: encoded))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            ModerationAppealEnvelope.self,
            from:
            JSONSerialization.data(withJSONObject: ["appeal": object])
        ))
    }

    func testSummaryDiscriminatorCannotHideProtectedFieldsOrMalformedSummary() throws {
        let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with:
            ApiFixtureLoader.data("native.moderation.appeals.default")) as? [String: Any])
        let summary = try XCTUnwrap((envelope["appeals"] as? [[String: Any]])?.first)
        for key in [
            "case_id",
            "appeal_reason",
            "internal_notes",
            "staff_context",
            "ai_draft_run_id",
            "ai_draft_requested_run_id",
            "ai_public_response",
            "drafted_at"
        ] {
            var mixed = summary
            mixed[key] = NSNull()
            XCTAssertThrowsError(
                try makeVouchaDecoder().decode(
                    ModerationAppealReadProjection.self,
                    from: JSONSerialization.data(withJSONObject: mixed)
                ),
                key
            )
        }
        var malformed = summary
        malformed.removeValue(forKey: "appellant_user_id")
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            ModerationAppealReadProjection.self,
            from: JSONSerialization.data(withJSONObject: malformed)
        ))
    }
}
