import Foundation
import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class EngineeringPostgresqlRangeTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/psql/jobs"
    private let selected = EngineeringClassifierRange(
        fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: 100
    )

    func testBothFamiliesAndActionsEnqueueExactlyOneRangeFromFrozenConfirmation() async throws {
        for family in NativePostgresqlRangeFamily.allCases {
            for type in [EngineeringPsqlJobType.createPartitions, .cleanupPartitions] {
                CannedFeedURLProtocol.reset()
                let model = try makeModel()
                var draft = selected
                model.prepareRange(family: family, type: type, range: draft)
                let confirmation = try XCTUnwrap(model.rangeConfirmation)
                XCTAssertEqual(confirmation.range, selected)
                XCTAssertEqual(confirmation.type, type)
                draft = EngineeringClassifierRange(fromDate: "2030-01-01", toDate: "2030-01-02")
                XCTAssertNotEqual(draft, confirmation.range)
                await model.confirmRangeOperation()
                let body = try requestBody()
                let field = family == .classifier ? "classifierRange" : "requestRange"
                XCTAssertEqual(body.count, 2)
                XCTAssertEqual(body["type"] as? String, type.rawValue)
                XCTAssertEqual(body[field] as? NSDictionary, [
                    "fromDate": "2020-02-01", "toDate": "2020-02-02", "maxDefaultRows": 100
                ] as NSDictionary)
                XCTAssertTrue(model.rangeQueued)
                XCTAssertNil(model.rangeConfirmation)
                XCTAssertTrue(model.actionLoadingKeys.isEmpty)
                XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
                XCTAssertNil(model.partitions) // Enqueued does not claim refreshed/completed partitions.
            }
        }
    }

    func testValidationRejectsInvalidCalendarOrderBudgetAndJobBeforeConfirmation() throws {
        let model = try makeModel()
        for range in [
            EngineeringClassifierRange(fromDate: "2021-02-29", toDate: "2021-03-01"),
            EngineeringClassifierRange(fromDate: "1969-12-31", toDate: "2020-01-01"),
            EngineeringClassifierRange(fromDate: "2020-02-02", toDate: "2020-02-02"),
            EngineeringClassifierRange(fromDate: "2020-02-03", toDate: "2020-02-02"),
            EngineeringClassifierRange(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: 0),
            EngineeringClassifierRange(fromDate: "2020-02-01", toDate: "2020-02-02", maxDefaultRows: 5_001)
        ] {
            model.prepareRange(family: .classifier, type: .createPartitions, range: range)
            XCTAssertNil(model.rangeConfirmation)
            XCTAssertEqual(model.rangeError, .invalidRange)
        }
        model.prepareRange(family: .request, type: .runMigrations, range: selected)
        XCTAssertNil(model.rangeConfirmation)
        XCTAssertEqual(model.rangeError, .invalidJobType)
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testCancelAndDuplicateConfirmationNeverSubmit() async throws {
        let model = try makeModel()
        model.prepareRange(family: .request, type: .cleanupPartitions, range: selected)
        model.prepareRange(family: .classifier, type: .createPartitions, range: selected)
        XCTAssertEqual(model.rangeConfirmation?.family, .request)
        XCTAssertEqual(model.rangeConfirmation?.type, .cleanupPartitions)
        await model.runJob(.createPartitions)
        model.cancelRangeOperation()
        await model.confirmRangeOperation()
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
        XCTAssertFalse(model.rangeQueued)
        XCTAssertNil(model.rangeConfirmation)
        XCTAssertTrue(model.actionLoadingKeys.isEmpty)
    }

    func testAccountSwitchRevocationAndCancellationDuringConfirmationNeverSubmit() async throws {
        let model = try makeModel()
        for change in 0 ... 2 {
            model.updateRangeAccess(accountId: "account", isAdministrator: true)
            model.prepareRange(family: .request, type: .createPartitions, range: selected)
            switch change {
            case 0: model.updateRangeAccess(accountId: "other", isAdministrator: true)
            case 1: model.updateRangeAccess(accountId: "account", isAdministrator: false)
            default: model.cancelRangeOperation()
            }
            await model.confirmRangeOperation()
            XCTAssertNil(model.rangeConfirmation)
            XCTAssertFalse(model.rangeQueued)
            XCTAssertTrue(model.actionLoadingKeys.isEmpty)
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testAuthorizationDefaultsClosed() async throws {
        let model = try NativeEngineeringPostgresqlViewModel(client: makeClient())
        model.prepareRange(family: .classifier, type: .createPartitions, range: selected)
        await model.confirmRangeOperation()
        XCTAssertFalse(model.isRangeAuthorized)
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.isEmpty)
    }

    func testServerFailureDoesNotAcknowledgeAndCanRetry() async throws {
        let model = try makeModel()
        CannedFeedURLProtocol.handlers[path] = (Data(), 403)
        model.prepareRange(family: .classifier, type: .cleanupPartitions, range: selected)
        await model.confirmRangeOperation()
        XCTAssertFalse(model.rangeQueued)
        XCTAssertNotNil(model.actionErrorMessage)
        stubAcknowledgement()
        model.prepareRange(family: .classifier, type: .cleanupPartitions, range: selected)
        await model.confirmRangeOperation()
        XCTAssertTrue(model.rangeQueued)
        XCTAssertNil(model.actionErrorMessage)
    }

    func testAccountSwitchWhilePOSTPendingDiscardsAcknowledgement() async throws {
        let model = try makeModel()
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        model.prepareRange(family: .request, type: .cleanupPartitions, range: selected)
        let action = Task { await model.confirmRangeOperation() }
        _ = try await barrier.wait()
        await model.confirmRangeOperation()
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["POST"])
        model.updateRangeAccess(accountId: "other", isAdministrator: true)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await action.value
        XCTAssertFalse(model.rangeQueued)
        XCTAssertNil(model.actionErrorMessage)
        XCTAssertTrue(model.actionLoadingKeys.isEmpty)
    }

    func testMonthlyConfirmationKeepsOmittedRangeRequest() async throws {
        let model = try makeModel()
        model.pendingAction = .createPartitions
        await model.confirmPendingAction()
        XCTAssertEqual(try requestBody(), ["type": "createPartitions"] as NSDictionary)
        XCTAssertNil(model.pendingAction)
        XCTAssertFalse(model.rangeQueued)
    }

    private func makeModel() throws -> NativeEngineeringPostgresqlViewModel {
        stubAcknowledgement()
        let model = try NativeEngineeringPostgresqlViewModel(client: makeClient())
        model.updateRangeAccess(accountId: "account", isAdministrator: true)
        return model
    }

    private func stubAcknowledgement() {
        CannedFeedURLProtocol.handlers[path] = (
            ApiFixtureLoader.data("web.admin.psql.jobs.classifier-range"), 200
        )
    }

    private func requestBody() throws -> NSDictionary {
        let body = try XCTUnwrap(CannedFeedURLProtocol.capturedBodies.first ?? nil)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: Data(body.utf8)) as? NSDictionary)
    }
}
