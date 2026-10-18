import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationDisputeRunIdentityTests: XCTestCase {
    func testStaffDisputeAcceptsNullAndPreservesRunIdentity() throws {
        var object = try disputeObject()
        try assertDisputeRoundTrip(object, expected: NSNull())
        object["ai_draft_run_id"] = "dispute-rerun-job-1"
        try assertDisputeRoundTrip(object, expected: "dispute-rerun-job-1")
    }

    func testMutationProjectionWithoutStaffRunIdentityIsAccepted() throws {
        let response = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.disputes.update.default")
        ) as? [String: Any])
        let object = try XCTUnwrap(response["dispute"] as? [String: Any])
        XCTAssertFalse(object.keys.contains("ai_draft_run_id"))
        let data = try JSONSerialization.data(withJSONObject: object)
        XCTAssertNil(try JSONDecoder.vouchaFixtureDecoder.decode(ModerationDispute.self, from: data).aiDraftRunId)
        XCTAssertNil(try JSONDecoder.vouchaFixtureDecoder.decode(ReviewDispute.self, from: data).aiDraftRunId)
    }

    func testQueuedRerunPreservesRequiredRunIdentity() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.resolution-drafts.default")
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(ReviewDisputeQueueResponse.self, from: data)
        XCTAssertEqual(response.runId, "dispute-rerun-job-1")
        let encoded = try JSONSerialization.jsonObject(with: JSONEncoder().encode(response)) as? [String: Any]
        XCTAssertEqual(encoded?["runId"] as? String, "dispute-rerun-job-1")
        var missing = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        missing.removeValue(forKey: "run_id")
        XCTAssertThrowsError(try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeQueueResponse.self, from: JSONSerialization.data(withJSONObject: missing)
        ))
    }

    private func disputeObject() throws -> [String: Any] {
        let response = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.disputes.detail.default")
        ) as? [String: Any])
        return try XCTUnwrap(response["dispute"] as? [String: Any])
    }

    private func assertDisputeRoundTrip(_ object: [String: Any], expected: Any) throws {
        let data = try JSONSerialization.data(withJSONObject: object)
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let moderation = try decoder.decode(ModerationDispute.self, from: data)
        let review = try decoder.decode(ReviewDispute.self, from: data)
        XCTAssertEqual(moderation.aiDraftRunId, expected as? String)
        XCTAssertEqual(review.aiDraftRunId, expected as? String)
        for encoded in try [JSONEncoder().encode(moderation), JSONEncoder().encode(review)] {
            let result = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
            if expected is NSNull {
                XCTAssertTrue(result["aiDraftRunId"] is NSNull)
            } else {
                XCTAssertEqual(result["aiDraftRunId"] as? String, expected as? String)
            }
        }
    }
}
