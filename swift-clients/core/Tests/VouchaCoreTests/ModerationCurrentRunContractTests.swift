import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationCurrentRunContractTests: XCTestCase {
    func testStaffDisputeRequiresRequestedAndSavedRunIdentities() throws {
        let object = try fixtureObject("native.moderation.disputes.detail.default", key: "dispute")
        try assertStaffIdentities(ModerationDispute.self, object: object)
        try assertStaffIdentities(ReviewDispute.self, object: object)
    }

    func testStaffDisputeDecodesRequestedRunStatusAndRequiresIt() throws {
        let object = try fixtureObject("native.moderation.disputes.detail.default", key: "dispute")
        XCTAssertEqual(object["ai_draft_requested_run_status"] as? String, "active")
        for type in [ModerationDispute.self as (Codable & Sendable).Type, ReviewDispute.self] {
            let encoded = try roundTrip(type, object: object)
            XCTAssertEqual(encoded["ai_draft_requested_run_status"] as? String, "active")
            var missing = object
            missing.removeValue(forKey: "ai_draft_requested_run_status")
            XCTAssertThrowsError(try decode(type, object: missing))
            var unknown = object
            unknown["ai_draft_requested_run_status"] = "later"
            XCTAssertThrowsError(try decode(type, object: unknown))
            for status in ["active", "succeeded", "failed", "expired"] {
                var row = object
                row["ai_draft_requested_run_status"] = status
                XCTAssertEqual(try roundTrip(type, object: row)["ai_draft_requested_run_status"] as? String, status)
            }
            var none = object
            none["ai_draft_requested_run_status"] = NSNull()
            XCTAssertTrue(try roundTrip(type, object: none)["ai_draft_requested_run_status"] is NSNull)
        }
        let review = try decode(ReviewDispute.self, object: object)
        XCTAssertEqual(review.aiDraftRequestedRunStatus, .active)
    }

    func testStaffAppealRequiresRequestedAndSavedRunIdentities() throws {
        let object = try fixtureObject("native.moderation.appeals.detail.default", key: "appeal")
        try assertStaffIdentities(ModerationAppeal.self, object: object, requestedNullable: true)
    }

    func testReportAdmissionRetainsRequiredRunIdentity() throws {
        let object = try fixtureObject("native.moderation.report-judgement.default")
        let encoded = try roundTrip(ModerationReportJudgementResponse.self, object: object)
        XCTAssertEqual(encoded["run_id"] as? String, object["run_id"] as? String)
        XCTAssertNotNil(encoded["run_id"])
        var missing = object
        missing.removeValue(forKey: "run_id")
        XCTAssertThrowsError(try decode(ModerationReportJudgementResponse.self, object: missing))
    }

    func testStaffReportRequiresAutomaticJudgementEligibility() throws {
        let list = try fixtureObject("native.moderation.reports.default")
        let rows = try XCTUnwrap(list["results"] as? [[String: Any]])
        let report = try XCTUnwrap(rows.first)
        try assertEligibility(StaffModerationReport.self, object: report)
        let community = try fixtureObject("native.community.pending-reports.paginated")
        let communityRows = try XCTUnwrap(community["reports"] as? [[String: Any]])
        try assertEligibility(CommunityPendingReport.self, object: XCTUnwrap(communityRows.first))
    }

    func testAdmissionsRejectNullRunIdentities() throws {
        var report = try fixtureObject("native.moderation.report-judgement.default")
        report["run_id"] = NSNull()
        XCTAssertThrowsError(try decode(ModerationReportJudgementResponse.self, object: report))
        var appeal = try fixtureObject("native.moderation.appeals.resolution-drafts.default")
        appeal["run_id"] = NSNull()
        XCTAssertThrowsError(try decode(ModerationAppealQueueResponse.self, object: appeal))
        var dispute = try fixtureObject("native.moderation.disputes.resolution-drafts.default")
        dispute["run_id"] = NSNull()
        XCTAssertThrowsError(try decode(ReviewDisputeQueueResponse.self, object: dispute))
    }

    private func assertStaffIdentities(
        _ type: (some Codable).Type, object: [String: Any], requestedNullable: Bool = false
    ) throws {
        let encoded = try roundTrip(type, object: object)
        for field in ["ai_draft_requested_run_id", "ai_draft_run_id"] {
            XCTAssertNotNil(object[field], field)
            XCTAssertEqual(encoded[field] as? String, object[field] as? String, field)
            var missing = object
            missing.removeValue(forKey: field)
            XCTAssertThrowsError(try decode(type, object: missing), field)
        }
        var nullRequested = object
        nullRequested["ai_draft_requested_run_id"] = NSNull()
        if requestedNullable {
            XCTAssertTrue(try roundTrip(type, object: nullRequested)["ai_draft_requested_run_id"] is NSNull)
        } else {
            XCTAssertThrowsError(try decode(type, object: nullRequested))
        }
        var nullableSaved = object
        nullableSaved["ai_draft_run_id"] = NSNull()
        XCTAssertTrue(try roundTrip(type, object: nullableSaved)["ai_draft_run_id"] is NSNull)
    }

    private func assertEligibility(_ type: (some Codable).Type, object: [String: Any]) throws {
        let encoded = try roundTrip(type, object: object)
        XCTAssertEqual(encoded["automatic_judgement_enabled"] as? Bool, object["automatic_judgement_enabled"] as? Bool)
        XCTAssertNotNil(encoded["automatic_judgement_enabled"])
        var missing = object
        missing.removeValue(forKey: "automatic_judgement_enabled")
        XCTAssertThrowsError(try decode(type, object: missing))
    }

    private func fixtureObject(_ id: String, key: String? = nil) throws -> [String: Any] {
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: ApiFixtureLoader.data(id)) as? [String: Any])
        return try key.map { try XCTUnwrap(object[$0] as? [String: Any]) } ?? object
    }

    private func decode<T: Decodable>(_ type: T.Type, object: [String: Any]) throws -> T {
        try JSONDecoder.vouchaFixtureDecoder.decode(type, from: JSONSerialization.data(withJSONObject: object))
    }

    private func roundTrip(_ type: (some Codable).Type, object: [String: Any]) throws -> [String: Any] {
        let value = try decode(type, object: object)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        return try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(value)) as? [String: Any])
    }
}
