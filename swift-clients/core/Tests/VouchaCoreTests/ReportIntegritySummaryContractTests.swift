import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ReportIntegritySummaryContractTests: XCTestCase {
    func testDetailRoundTripPreservesNullableContextAndResolutionFields() throws {
        let detail = try makeVouchaDecoder().decode(
            ReportIntegrityFlagEnvelope.self,
            from: ApiFixtureLoader.data("native.moderation.report-integrity.flag")
        )
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.dateEncodingStrategy = .iso8601
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(detail.flag)) as? [String: Any])

        XCTAssertEqual(object["entity_label"] as? String, "Reported post")
        for field in [
            "reported_username",
            "resolver_display_name",
            "resolver_username",
            "resolution",
            "resolved_at",
            "resolved_by_id"
        ] {
            XCTAssertTrue(object[field] is NSNull, field)
        }
    }

    func testReportIntegrityListAndDetailKeepReporterIdsOnDetailOnly() throws {
        let list = try makeVouchaDecoder().decode(
            ReportIntegrityFlagsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.report-integrity.default")
        )
        XCTAssertGreaterThan(try XCTUnwrap(list.results.first).reporterCount, 0)
        XCTAssertNil(list.results.first?.details["reporter_user_ids"])
        let detail = try makeVouchaDecoder().decode(
            ReportIntegrityFlagEnvelope.self,
            from: ApiFixtureLoader.data("native.moderation.report-integrity.flag")
        )
        guard case let .array(ids) = detail.flag.details["reporter_user_ids"] else {
            return XCTFail("Detail must expose reporter ids")
        }
        XCTAssertFalse(ids.isEmpty)
    }
}
