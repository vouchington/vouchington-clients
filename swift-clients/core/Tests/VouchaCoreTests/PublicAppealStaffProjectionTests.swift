import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class PublicAppealStaffProjectionTests: XCTestCase {
    func testCanonicalStaffReadRetainsRequestedAndSavedIdentity() throws {
        let response = try makeVouchaDecoder().decode(
            MemberAppealReadResponse.self,
            from: ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        )
        let projection = response.appeal
        guard case let .staff(appeal) = projection else {
            return XCTFail("Staff data must not downgrade to the member projection")
        }
        XCTAssertEqual(appeal.aiDraftRequestedRunId?.isEmpty, false)
        XCTAssertNotNil(appeal.caseId)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let data = try encoder.encode(projection)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        XCTAssertEqual(object["ai_draft_requested_run_id"] as? String, appeal.aiDraftRequestedRunId)
        XCTAssertEqual(object["ai_draft_requested_run_status"] as? String, "succeeded")
        XCTAssertEqual(appeal.aiDraftRequestedRunStatus, .succeeded)
        XCTAssertNotNil(object["ai_draft_run_id"])
        XCTAssertNotNil(object["internal_notes"])
    }

    func testMalformedStaffCannotLoseRequiredFieldsAndBecomeMember() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let appeal = try XCTUnwrap(envelope["appeal"] as? [String: Any])
        for key in [
            "ai_draft_requested_run_id", "ai_draft_requested_run_status", "ai_draft_run_id",
            "internal_notes", "drafted_at",
            "edited_at", "edited_by_id", "approved_by_id", "resolved_by_id", "latest_lifecycle_change_id"
        ] {
            var malformed = appeal
            malformed.removeValue(forKey: key)
            XCTAssertThrowsError(try makeVouchaDecoder().decode(
                ModerationAppealReadProjection.self,
                from: JSONSerialization.data(withJSONObject: malformed)
            ), key)
        }
    }

    func testStaffAppealWithoutAnAdmittedRunStaysStaff() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.detail.default")
        let envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var appeal = try XCTUnwrap(envelope["appeal"] as? [String: Any])
        appeal["ai_draft_requested_run_id"] = NSNull()
        let projection = try makeVouchaDecoder().decode(
            ModerationAppealReadProjection.self,
            from: JSONSerialization.data(withJSONObject: appeal)
        )
        guard case let .staff(staff) = projection else {
            return XCTFail("A null requested run must not downgrade staff data to the member projection")
        }
        XCTAssertNil(staff.aiDraftRequestedRunId)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(projection)) as? [String: Any])
        XCTAssertTrue(object["ai_draft_requested_run_id"] is NSNull)
    }

}
