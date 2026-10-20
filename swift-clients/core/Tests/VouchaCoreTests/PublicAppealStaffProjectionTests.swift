import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class PublicAppealStaffProjectionTests: XCTestCase {
    func testCanonicalStaffReadRetainsRequestedAndSavedIdentity() throws {
        let response = try makeVouchaDecoder().decode(
            MemberAppealListResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.default")
        )
        let projection = try XCTUnwrap(response.appeals.first)
        guard case let .staff(appeal) = projection else {
            return XCTFail("Staff data must not downgrade to the member projection")
        }
        XCTAssertFalse(appeal.aiDraftRequestedRunId.isEmpty)
        XCTAssertNotNil(appeal.caseId)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let data = try encoder.encode(projection)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(object["ai_draft_requested_run_id"] as? String, appeal.aiDraftRequestedRunId)
        XCTAssertNotNil(object["ai_draft_run_id"])
        XCTAssertNotNil(object["internal_notes"])
    }

    func testMalformedStaffCannotLoseRequiredFieldsAndBecomeMember() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.default")
        let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let appeal = try XCTUnwrap((envelope["appeals"] as? [[String: Any]])?.first)
        for key in [
            "ai_draft_requested_run_id", "ai_draft_run_id", "internal_notes", "drafted_at",
            "edited_at", "edited_by_id", "approved_by_id", "resolved_by_id", "latest_lifecycle_change_id"
        ] {
            var malformed = appeal
            malformed.removeValue(forKey: key)
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                ModerationAppealReadProjection.self,
                from: JSONSerialization.data(withJSONObject: malformed)
            ), key)
        }
        var nullRequested = appeal
        nullRequested["ai_draft_requested_run_id"] = NSNull()
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            ModerationAppealReadProjection.self,
            from: JSONSerialization.data(withJSONObject: nullRequested)
        ))
    }
}
