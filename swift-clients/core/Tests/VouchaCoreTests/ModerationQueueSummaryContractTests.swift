import Foundation
import VouchaModels
import XCTest

final class ModerationQueueSummaryContractTests: XCTestCase {
    func testAppealQueueRetainsDraftPresenceAndAppellantWithoutDetailIdentity() throws {
        let data = ApiFixtureLoader.data("native.moderation.appeals.default")
        let response = try makeVouchaDecoder().decode(ModerationAppealListResponse.self, from: data)
        let summary = try XCTUnwrap(response.appeals.first)
        XCTAssertEqual(summary.appellant.username, "appealing-reviewer")
        XCTAssertTrue(summary.hasAiDraft)
        XCTAssertFalse(summary.hasDraft)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let summaries = try XCTUnwrap(object["appeals"] as? [[String: Any]])
        let detailData = try JSONSerialization.data(withJSONObject: XCTUnwrap(summaries.first))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(ModerationAppeal.self, from: detailData))
    }

    func testDisputeQueuePreservesPublicLabelsAndDoesNotSubstituteForDetail() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.default")
        let response = try makeVouchaDecoder().decode(ReviewDisputeListResponse.self, from: data)
        let summary = try XCTUnwrap(response.disputes.first)
        XCTAssertEqual(summary.disputant.username, "native-claimant")
        XCTAssertEqual(summary.postLabel, "A disputed review")
        XCTAssertEqual(summary.postSlug, "a-disputed-review")
        XCTAssertFalse(summary.hasDraft)
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let summaries = try XCTUnwrap(object["disputes"] as? [[String: Any]])
        let detailData = try JSONSerialization.data(withJSONObject: XCTUnwrap(summaries.first))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(ReviewDispute.self, from: detailData))
    }

    func testMalformedSummaryCannotBecomeAnActionableDetail() throws {
        let data = ApiFixtureLoader.data("native.moderation.disputes.default")
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var summaries = try XCTUnwrap(object["disputes"] as? [[String: Any]])
        summaries[0].removeValue(forKey: "has_ai_draft")
        object["disputes"] = summaries
        let invalid = try JSONSerialization.data(withJSONObject: object)
        XCTAssertThrowsError(try makeVouchaDecoder().decode(ReviewDisputeListResponse.self, from: invalid))
    }
}
