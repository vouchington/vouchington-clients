import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class ReportIntegritySummaryContractTests: XCTestCase {
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
