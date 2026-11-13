import Foundation
import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class EngineeringPostgresqlRangeControlsTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/psql/jobs"
    private let locale = Locale(identifier: "en")

    func testAdministratorSeesBothFamiliesWithTheirOwnCopy() throws {
        let model = try makeModel(administrator: true)
        let inspection = try NativeEngineeringPostgresqlRangeSection(viewModel: model).inspect()
        XCTAssertEqual(try inspection.findAll(ViewType.TextField.self).count, 6)
        for key in [
            UiMessageKey.engineeringClassifierRangeCreate, .engineeringClassifierRangeRetire,
            .engineeringRequestRangeCreate, .engineeringRequestRangeRetire
        ] {
            XCTAssertNoThrow(try inspection.find(button: UiMessages.string(key, locale: locale)))
        }
    }

    func testNonAdministratorSeesNoRangeControls() throws {
        let model = try makeModel(administrator: false)
        let view = NativeEngineeringPostgresqlRangeSection(viewModel: model)
        XCTAssertThrowsError(try view.inspect().find(ViewType.TextField.self))
        XCTAssertThrowsError(try view.inspect().find(ViewType.Button.self))
    }

    func testButtonsFreezeDraftAndShowFamilySpecificConfirmationCopy() throws {
        let classifier = NativePostgresqlRangeKeys.classifier
        let request = NativePostgresqlRangeKeys.request
        let cases: [(NativePostgresqlRangeFamily, UiMessageKey, UiMessageKey, EngineeringPsqlJobType)] = [
            (.classifier, classifier.create, classifier.confirmCreate, .createPartitions),
            (.classifier, classifier.retire, classifier.confirmRetire, .cleanupPartitions),
            (.request, request.create, request.confirmCreate, .createPartitions),
            (.request, request.retire, request.confirmRetire, .cleanupPartitions)
        ]
        for (family, button, confirm, type) in cases {
            let model = try makeModel(administrator: true)
            model.rangeDrafts[family] = .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "50")
            let view = NativeEngineeringPostgresqlRangeSection(viewModel: model)
            try view.inspect().find(button: UiMessages.string(button, locale: locale)).tap()
            XCTAssertEqual(model.rangeConfirmation?.family, family)
            XCTAssertEqual(model.rangeConfirmation?.type, type)
            XCTAssertEqual(model.rangeConfirmation?.range.maxDefaultRows, type == .createPartitions ? 50 : nil)
            XCTAssertEqual(
                view.dialogMessage,
                UiMessages.string(
                    confirm,
                    parameters: ["fromDate": "2020-02-01", "toDate": "2020-02-02"],
                    locale: locale
                )
            )
            XCTAssertEqual(view.dialogTitle, UiMessages.string(button, locale: locale))
            XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
        }
    }

    func testConfirmationCopyIsDistinctBetweenFamiliesAndActions() {
        let keys = [NativePostgresqlRangeKeys.classifier, .request]
        let all = keys.flatMap { [$0.confirmCreate, $0.confirmRetire, $0.create, $0.retire] }
        XCTAssertEqual(Set(all.map { UiMessages.string($0, locale: locale) }).count, all.count)
    }

    func testInvalidDraftsShowFamilyScopedErrorAndNeverConfirm() throws {
        let invalid = [
            NativePostgresqlRangeDraft(fromDate: "", toDate: ""),
            .init(fromDate: "2020-02-02", toDate: "2020-02-02"),
            .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "0"),
            .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "5001"),
            .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "1e3"),
            .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "+5"),
            .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "-5")
        ]
        for draft in invalid {
            let model = try makeModel(administrator: true)
            model.rangeDrafts[.request] = draft
            let view = NativeEngineeringPostgresqlRangeSection(viewModel: model)
            try view.inspect().find(button: UiMessages.string(.engineeringRequestRangeCreate, locale: locale)).tap()
            XCTAssertNil(model.rangeConfirmation)
            XCTAssertEqual(model.rangeError, .invalidRange)
            XCTAssertEqual(model.rangeErrorFamily, .request)
            let invalidText = UiMessages.string(.engineeringRequestRangeInvalidRange, locale: locale)
            // The English copy is identical across families, so exactly one visible message proves scoping.
            XCTAssertEqual(try view.inspect().findAll(ViewType.Text.self).filter {
                (try? $0.string()) == invalidText
            }.count, 1)
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testRetireIgnoresInvalidBudgetAndOmitsIt() throws {
        let model = try makeModel(administrator: true)
        model.rangeDrafts[.classifier] = .init(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: "abc")
        model.prepareRange(family: .classifier, retire: true)
        XCTAssertNil(model.rangeError)
        XCTAssertNil(model.rangeConfirmation?.range.maxDefaultRows)
    }

    func testConfirmTapSubmitsDespiteDialogDismissalAndDismissalAloneCancels() async throws {
        let model = try makeModel(administrator: true)
        model.rangeDrafts[.classifier] = .init(fromDate: "2020-02-01", toDate: "2020-02-02")
        model.prepareRange(family: .classifier, retire: false)
        model.dismissRangeConfirmation()
        XCTAssertNil(model.rangeConfirmation)
        XCTAssertTrue(model.actionLoadingKeys.isEmpty)

        model.prepareRange(family: .classifier, retire: false)
        model.requestRangeConfirmation()
        model.dismissRangeConfirmation()
        XCTAssertNotNil(model.rangeConfirmation)
        await model.confirmRangeOperation()
        XCTAssertTrue(model.rangeQueued)
        XCTAssertFalse(model.rangeConfirmRequested)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first ?? nil)
        let json = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(body.utf8)) as? NSDictionary)
        XCTAssertEqual(json["classifierRange"] as? NSDictionary, [
            "fromDate": "2020-02-01", "toDate": "2020-02-02"
        ] as NSDictionary)
        XCTAssertNil(json["requestRange"])
    }

    func testRequestConfirmationWithoutSelectionIsInert() throws {
        let model = try makeModel(administrator: true)
        model.requestRangeConfirmation()
        XCTAssertFalse(model.rangeConfirmRequested)
        let view = NativeEngineeringPostgresqlRangeSection(viewModel: model)
        XCTAssertEqual(view.dialogTitle, "")
        XCTAssertEqual(view.dialogMessage, "")
        XCTAssertNil(view.dialogRole)
    }

    private func makeModel(administrator: Bool) throws -> NativeEngineeringPostgresqlViewModel {
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("web.admin.psql.jobs.classifier-range"), 200
        )
        let model = try NativeEngineeringPostgresqlViewModel(client: makeClient())
        model.updateRangeAccess(accountId: "account", isAdministrator: administrator)
        return model
    }
}
