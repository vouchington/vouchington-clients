import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealsDetailMutationRaceTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ModerationAppealsTestSupport

    func testReopenedDetailCannotReplaceCompletedDeliveryWithHeldUnsentSnapshot() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 200)
        let current = try XCTUnwrap(model.detailModerationAppeal)
        XCTAssertNotNil(current.sentAt)
        XCTAssertNotNil(model.appeals.first?.sentAt)
        XCTAssertFalse(model.canSend(current))
        XCTAssertFalse(model.isLoadingDetail)
        XCTAssertNil(model.detailError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/appeals/appeal-1/delivery"), 1)
    }

    func testReopenedDetailFailureCannotHideCompletedDelivery() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 500)
        XCTAssertNotNil(model.detailModerationAppeal?.sentAt)
        XCTAssertNil(model.detailError)
        XCTAssertFalse(model.isLoadingDetail)
    }

    func testDeliveryForAnotherRowDoesNotDiscardReopenedDetail() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 200, reopenedId: "appeal-2")
        let selected = try XCTUnwrap(model.detailModerationAppeal)
        XCTAssertEqual(selected.id, "appeal-2")
        XCTAssertNil(selected.sentAt)
        XCTAssertNotNil(model.appeals.first { $0.id == "appeal-1" }?.sentAt)
        XCTAssertFalse(model.isLoadingDetail)
        XCTAssertNil(model.detailError)
    }

    private func deliveredModelAfterReopenedRead(
        statusCode: Int,
        reopenedId: String = "appeal-1"
    ) async throws -> ModerationAppealsViewModel {
        let model = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        let detailPath = "/api/v1/appeals/appeal-1"
        let deliveryPath = detailPath + "/delivery"
        let approved = Support.appeal(publicResponse: "Approved response", approvedAt: "2026-07-01T11:00:00Z")
        CannedFeedURLProtocol.handlers[detailPath] = (Support.envelope(approved), 200)
        await model.loadDetail(id: "appeal-1")
        let original = try XCTUnwrap(model.detailModerationAppeal)
        CannedFeedURLProtocol.handlers[deliveryPath] = (Support.envelope(Support.appeal(
            publicResponse: "Approved response",
            approvedAt: "2026-07-01T11:00:00Z",
            sentAt: "2026-07-01T12:00:00Z"
        )), 200)
        CannedFeedURLProtocol.suspendResponse(path: deliveryPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: deliveryPath) }
        let delivery = Task { await model.send(original) }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: deliveryPath) else {
            CannedFeedURLProtocol.releaseResponse(path: deliveryPath)
            await delivery.value
            return model
        }
        let reopenedPath = "/api/v1/appeals/\(reopenedId)"
        let reopened = Support.appeal(
            id: reopenedId, publicResponse: "Approved response", approvedAt: "2026-07-01T11:00:00Z"
        )
        CannedFeedURLProtocol.handlers[reopenedPath] = statusCode == 200
            ? (Support.envelope(reopened), 200)
            : (Data(#"{"error":"detail unavailable"}"#.utf8), statusCode)
        CannedFeedURLProtocol.suspendResponse(path: reopenedPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: reopenedPath) }
        let reopening = Task { await model.loadDetail(id: reopenedId) }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: reopenedPath) else {
            CannedFeedURLProtocol.releaseResponse(path: deliveryPath)
            CannedFeedURLProtocol.releaseResponse(path: reopenedPath)
            await delivery.value
            await reopening.value
            return model
        }
        CannedFeedURLProtocol.releaseResponse(path: deliveryPath)
        await delivery.value
        XCTAssertNotNil(model.appeals.first { $0.id == "appeal-1" }?.sentAt)
        CannedFeedURLProtocol.releaseResponse(path: reopenedPath)
        await reopening.value
        return model
    }
}
