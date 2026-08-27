import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationTransparencyModelTests: XCTestCase {
    func testDecodingRetainsKnownValues() throws {
        let response = try decode(range: "30d", metric: "reports", nextCursor: "older-page")

        XCTAssertEqual(response.range, .days30)
        XCTAssertEqual(response.buckets.first?.metric, .reports)
        XCTAssertEqual(response.nextCursor, "older-page")
    }

    func testDecodingToleratesUnknownRangeAndMetric() throws {
        let response = try decode(range: "future", metric: "future_metric")

        XCTAssertEqual(response.range, .unknown)
        XCTAssertEqual(response.buckets.first?.metric, .unknown)
    }

    private func decode(
        range: String,
        metric: String,
        nextCursor: String? = nil
    ) throws -> ModerationTransparency {
        let cursorJSON = nextCursor.map { ",\"next_cursor\":\"\($0)\"" } ?? ""
        return try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationTransparency.self,
            from: Data(
                #"{"range":"\#(range)","buckets":[{"date":"2026-08-01","metric":"\#(metric)","category":"all","count":20}]\#(cursorJSON)}"#
                    .utf8
            )
        )
    }
}
