import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationReportRunLookupContractTests: XCTestCase {
    func testPendingLookupRetainsRequiredCompletionAndNullableJudgementIdentity() throws {
        let data = ApiFixtureLoader.data("native.moderation.report-judgement-run.pending")
        let response = try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationReportJudgementRunResponse.self,
            from: data
        )
        XCTAssertFalse(response.completed)
        XCTAssertNil(response.judgementId)
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(response)) as? [String: Any])
        XCTAssertEqual(encoded["completed"] as? Bool, false)
        XCTAssertTrue(encoded["judgement_id"] is NSNull)
        let original = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        for key in ["completed", "judgement_id"] {
            var missing = original
            missing.removeValue(forKey: key)
            XCTAssertThrowsError(try JSONDecoder.vouchaFixtureDecoder.decode(
                ModerationReportJudgementRunResponse.self,
                from: JSONSerialization.data(withJSONObject: missing)
            ), key)
        }
    }

    func testLookupUsesBothCanonicalReportAndAdmittedRunIdentity() throws {
        let fixture = try XCTUnwrap(ApiFixtureLoader.swiftRouteFixtures.first {
            $0.id == "native.moderation.report-judgement-run.pending"
        })
        let endpoint = Endpoint.moderationReportJudgementRun(
            reportId: moderationFlatReportId,
            runId: "00000000-0000-7000-8000-000000000303"
        )
        assertEndpoint(endpoint, path: fixture.path)
        XCTAssertEqual(endpoint.method.rawValue, fixture.method)
    }
}
