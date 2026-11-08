import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesDetailMutationRaceTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport

    func testReopenedDetailCannotReplaceCompletedDeliveryWithHeldUnsentSnapshot() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 200)
        let current = try XCTUnwrap(model.detailReviewDispute)
        XCTAssertNotNil(current.sentAt)
        XCTAssertNotNil(model.disputes.first?.sentAt)
        XCTAssertFalse(model.canDeliver(current))
        XCTAssertFalse(model.isLoadingDetail)
        XCTAssertNil(model.detailError)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1/delivery"), 1)
    }

    func testReopenedDetailFailureCannotHideCompletedDelivery() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 500)
        XCTAssertNotNil(model.detailReviewDispute?.sentAt)
        XCTAssertNil(model.detailError)
        XCTAssertFalse(model.isLoadingDetail)
    }

    func testDeliveryForAnotherRowDoesNotDiscardReopenedDetail() async throws {
        let model = try await deliveredModelAfterReopenedRead(statusCode: 200, reopenedId: "dispute-2")
        let selected = try XCTUnwrap(model.detailReviewDispute)
        XCTAssertEqual(selected.id, "dispute-2")
        XCTAssertNil(selected.sentAt)
        XCTAssertNotNil(model.disputes.first { $0.id == "dispute-1" }?.sentAt)
        XCTAssertFalse(model.isLoadingDetail)
        XCTAssertNil(model.detailError)
    }

    private func deliveredModelAfterReopenedRead(
        statusCode: Int,
        reopenedId: String = "dispute-1"
    ) async throws -> ReviewDisputesViewModel {
        let model = try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        let detailPath = "/api/v1/disputes/dispute-1"
        let deliveryPath = detailPath + "/delivery"
        let approved = Support.dispute(publicResponse: "Approved response", approvedAt: "2026-07-01T11:00:00Z")
        CannedFeedURLProtocol.handlers[detailPath] = (Support.envelope(approved), 200)
        await model.loadDetail(id: "dispute-1")
        let original = try XCTUnwrap(model.detailReviewDispute)
        CannedFeedURLProtocol.handlers[deliveryPath] = (Support.envelope(Support.dispute(
            publicResponse: "Approved response",
            approvedAt: "2026-07-01T11:00:00Z",
            sentAt: "2026-07-01T12:00:00Z"
        )), 200)
        CannedFeedURLProtocol.suspendResponse(path: deliveryPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: deliveryPath) }
        let delivery = Task { await model.deliver(original) }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: deliveryPath) else {
            CannedFeedURLProtocol.releaseResponse(path: deliveryPath)
            await delivery.value
            return model
        }
        let reopenedPath = "/api/v1/disputes/\(reopenedId)"
        let reopened = Support.dispute(
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
        XCTAssertNotNil(model.disputes.first { $0.id == "dispute-1" }?.sentAt)
        CannedFeedURLProtocol.releaseResponse(path: reopenedPath)
        await reopening.value
        return model
    }
}
