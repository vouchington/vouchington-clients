import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationReportJudgementIdentityTests: XCTestCase {
    func testStaffReportFixtureRetainsRequiredJudgementIdentity() throws {
        let page = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffFlatModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.default")
        )
        let judgement = try XCTUnwrap(page.reports.first?.judgement)
        let encoded = try JSONEncoder().encode(judgement)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])
        XCTAssertEqual(object["id"] as? String, "00000000-0000-7000-8000-000000000301")
    }

    func testNullJudgementIdentityIsRejected() throws {
        var object = try judgementObject()
        object["id"] = NSNull()
        XCTAssertThrowsError(try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationReportJudgement.self,
            from: JSONSerialization.data(withJSONObject: object)
        ))
    }

    func testMissingJudgementIdentityIsRejected() throws {
        var object = try judgementObject()
        object.removeValue(forKey: "id")
        let data = try JSONSerialization.data(withJSONObject: object)
        XCTAssertThrowsError(try JSONDecoder.vouchaFixtureDecoder.decode(ModerationReportJudgement.self, from: data))
    }

    func testSameTextJudgementsRetainDistinctIdentities() throws {
        let original = try judgementObject()
        var rerun = original
        rerun["id"] = "00000000-0000-7000-8000-000000000302"
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let first = try decoder.decode(
            ModerationReportJudgement.self,
            from: JSONSerialization.data(withJSONObject: original)
        )
        let second = try decoder.decode(
            ModerationReportJudgement.self,
            from: JSONSerialization.data(withJSONObject: rerun)
        )
        XCTAssertEqual(first.publicResponse, second.publicResponse)
        let encoder = JSONEncoder()
        let firstJSON = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(first)) as? [String: Any])
        let secondJSON = try XCTUnwrap(JSONSerialization.jsonObject(with: encoder.encode(second)) as? [String: Any])
        XCTAssertEqual(firstJSON["id"] as? String, "00000000-0000-7000-8000-000000000301")
        XCTAssertEqual(secondJSON["id"] as? String, "00000000-0000-7000-8000-000000000302")
    }

    private func judgementObject() throws -> [String: Any] {
        let page = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("native.moderation.reports.default")
        ) as? [String: Any])
        let results = try XCTUnwrap(page["results"] as? [[String: Any]])
        return try XCTUnwrap(results.first?["judgement"] as? [String: Any])
    }
}
