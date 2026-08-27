import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

final class PointValuationDraftTests: XCTestCase {
    @MainActor
    func testSurfaceInteractionStateUpdatesLocaleOnlyBeforeValueEntry() throws {
        let french = Locale(identifier: "fr_FR")
        let emptyState = PointValuationsSurfaceInteractionState(locale: Locale(identifier: "en_US"))
        emptyState.createDraft.note = "Keep this note"
        emptyState.updateCreateDraftLocaleIfValueEmpty(french)
        emptyState.createDraft.valuePerPointText = "1,5"

        let localizedBody = try emptyState.createDraft.createBody(rewardsProgramId: "program-1")

        XCTAssertEqual(localizedBody.valuePerPoint.amount, 1_500_000)
        XCTAssertEqual(localizedBody.note, "Keep this note")

        let typedState = PointValuationsSurfaceInteractionState(locale: Locale(identifier: "en_US"))
        typedState.createDraft.valuePerPointText = "1.5"
        typedState.createDraft.note = "Keep this draft"
        typedState.updateCreateDraftLocaleIfValueEmpty(french)

        let preservedBody = try typedState.createDraft.createBody(rewardsProgramId: "program-1")
        XCTAssertEqual(typedState.createDraft.note, "Keep this draft")
        XCTAssertEqual(preservedBody.valuePerPoint.amount, 1_500_000)
    }

    func testLocalizedDraftFormatsWithoutTrailingZerosAndEncodesNumber() throws {
        let valuation = try pointValuation(value: XCTUnwrap(Decimal(string: "1.5000")))
        var draft = PointValuationDraft(valuation: valuation, locale: Locale(identifier: "fr_FR"))

        XCTAssertEqual(draft.valuePerPointText, "1,5")
        draft.valuePerPointText = "1 234,5"
        let body = try XCTUnwrap(draft.updateBody(comparedWith: valuation))
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: JSONEncoder().encode(body)) as? [String: Any]
        )
        let value = try XCTUnwrap(object["valuePerPoint"] as? [String: Any])
        XCTAssertEqual(value["amount"] as? NSNumber, 1_234_500_000)
        XCTAssertEqual(value["currency"] as? String, "usd")
        XCTAssertEqual(value["scale"] as? NSNumber, 6)
    }

    func testDraftEncodesMicrocentUsdValueAndCanChangeToJpy() throws {
        var usd = PointValuationDraft(locale: Locale(identifier: "en_US"))
        usd.valuePerPointText = "0.035"
        XCTAssertEqual(try usd.createBody(rewardsProgramId: "program").valuePerPoint.amount, 35_000)

        let valuation = pointValuation(value: 1)
        var jpy = PointValuationDraft(valuation: valuation, locale: Locale(identifier: "en_US"))
        jpy.currency = "jpy"
        let patch = try XCTUnwrap(jpy.updateBody(comparedWith: valuation))
        XCTAssertEqual(patch.valuePerPoint?.currency, "jpy")
        XCTAssertEqual(patch.valuePerPoint?.amount, 1_000_000)
    }

    func testDraftUsesCanonicalScaledMoneyScale() throws {
        var draft = PointValuationDraft(locale: Locale(identifier: "en_US"))
        draft.valuePerPointText = "0." + String(repeating: "0", count: ScaledMoney.scale - 1) + "1"
        XCTAssertEqual(try draft.createBody(rewardsProgramId: "program").valuePerPoint.amount, 1)

        draft.valuePerPointText = "0." + String(repeating: "0", count: ScaledMoney.scale) + "1"
        XCTAssertThrowsError(try draft.createBody(rewardsProgramId: "program"))
    }

    func testDraftEnforcesSourceTextScaleUsingLocalizedDecimalSeparator() throws {
        for (localeIdentifier, accepted, rejected) in [
            ("en_US", "1.000000", "1.0000000"),
            ("fr_FR", "1,000000", "1,0000000")
        ] {
            var draft = PointValuationDraft(locale: Locale(identifier: localeIdentifier))
            draft.valuePerPointText = accepted
            XCTAssertNoThrow(try draft.createBody(rewardsProgramId: "program"))

            draft.valuePerPointText = rejected
            XCTAssertThrowsError(try draft.createBody(rewardsProgramId: "program"))
        }
    }

    func testDraftEnforcesPointValuationBusinessMaximum() throws {
        var draft = PointValuationDraft(locale: Locale(identifier: "en_US"))
        draft.valuePerPointText = "9999.999999"
        XCTAssertEqual(
            try draft.createBody(rewardsProgramId: "program").valuePerPoint.amount,
            PointValuation.maximumValuePerPointAmount
        )

        draft.valuePerPointText = "10000"
        XCTAssertThrowsError(try draft.createBody(rewardsProgramId: "program"))
    }

    func testDraftRejectsNegativeAndPartiallyParsedValues() throws {
        let valuation = pointValuation(value: 1)
        for text in ["-1", "12abc", "1.2.3", ""] {
            var draft = PointValuationDraft(valuation: valuation, locale: Locale(identifier: "en_US"))
            draft.valuePerPointText = text
            XCTAssertThrowsError(try draft.updateBody(comparedWith: valuation))
        }

        var zero = PointValuationDraft(valuation: valuation, locale: Locale(identifier: "en_US"))
        zero.valuePerPointText = "0"
        XCTAssertNoThrow(try zero.updateBody(comparedWith: valuation))

        var highPrecision = PointValuationDraft(valuation: valuation, locale: Locale(identifier: "en_US"))
        highPrecision.valuePerPointText = "1.23456"
        XCTAssertNoThrow(try highPrecision.updateBody(comparedWith: valuation))
    }

    func testNoOpAndNoteClearingPatches() throws {
        let valuation = try pointValuation(value: XCTUnwrap(Decimal(string: "1.5")), note: "Original")
        XCTAssertNil(try PointValuationDraft(valuation: valuation).updateBody(comparedWith: valuation))

        var draft = PointValuationDraft(valuation: valuation)
        draft.note = ""
        let body = try XCTUnwrap(draft.updateBody(comparedWith: valuation))
        let object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: JSONEncoder().encode(body)) as? [String: Any]
        )
        XCTAssertTrue(object["note"] is NSNull)
        XCTAssertNil(object["valuePerPoint"])
    }

    private func pointValuation(value: Decimal, note: String? = nil) -> PointValuation {
        PointValuation(
            id: "valuation-1",
            rewardsProgramId: "program-1",
            valuePerPoint: try! ScaledMoney(
                amount: NSDecimalNumber(decimal: value * 1_000_000).int64Value,
                currency: "usd"
            ),
            note: note,
            rewardsProgram: .init(id: "program-1", name: "Example Rewards", slug: "example")
        )
    }
}
