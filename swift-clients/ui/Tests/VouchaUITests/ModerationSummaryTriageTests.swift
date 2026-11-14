import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ModerationSummaryTriageTests: XCTestCase {
    func testDisputeSummaryShowsLocalizedOverdueAndRecommendation() throws {
        var object = try summaryObject("native.moderation.disputes.default", key: "disputes")
        object["is_overdue"] = true
        object["recommended_action"] = "remove"
        let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeSummary.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let inspection = try StaffModerationSummaryRow(dispute: summary).inspect()
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(
            .nativeSwiftReviewDisputesOverdue,
            locale: .current
        )))
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(
            .nativeSwiftReviewDisputesAiRecommendation,
            parameters: ["recommendation": UiMessages
                .string(ReviewDisputeAction.remove.titleKey, locale: .current)],
            locale: .current
        )))
    }

    func testAppealSummaryShowsLocalizedOverdueAndRecommendation() throws {
        var object = try summaryObject("native.moderation.appeals.default", key: "appeals")
        object["is_overdue"] = true
        object["recommended_action"] = "reduce"
        let summary = try JSONDecoder.vouchaFixtureDecoder.decode(
            ModerationAppealSummary.self,
            from: JSONSerialization.data(withJSONObject: object)
        )
        let inspection = try StaffModerationSummaryRow(appeal: summary).inspect()
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(
            .nativeSwiftModerationAppealsOverdue,
            locale: .current
        )))
        XCTAssertNoThrow(try inspection.find(text: UiMessages.string(
            .nativeSwiftModerationAppealsAiRecommendation,
            parameters: ["recommendation": UiMessages
                .string(ModerationAppealAction.reduce.titleKey, locale: .current)],
            locale: .current
        )))
    }

    private func summaryObject(_ fixture: String, key: String) throws -> [String: Any] {
        let envelope = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data(fixture)) as? [String: Any])
        return try XCTUnwrap((envelope[key] as? [[String: Any]])?.first)
    }
}
