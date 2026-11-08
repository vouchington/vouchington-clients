import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SummaryDetailTests: NativeRouteSurfaceViewModelTestCase {
    func testDisputeListDoesNotLoadStaffFieldsUntilDetailOpens() async throws {
        let detailPath = "/api/v1/disputes/00000000-0000-7000-8000-000000000201"
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            ApiFixtureLoader.data("native.moderation.disputes.default"), 200
        )
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.moderation.disputes.detail.default"), 200
        )
        let model = try disputeModel()
        await model.load()
        XCTAssertEqual(model.summaryPagination.items.count, 1)
        XCTAssertTrue(model.disputes.isEmpty)
        XCTAssertTrue(model.publicResponseDrafts.isEmpty)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(detailPath), 0)
        let summary = try XCTUnwrap(model.summaryPagination.items.first)
        let list = try ReviewDisputesSurface(viewModel: model).inspect()
        XCTAssertNoThrow(try list.find(text: "native-claimant"))
        XCTAssertThrowsError(try list.find(button: "Approve"))
        CannedFeedURLProtocol.suspendResponse(path: detailPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: detailPath) }
        let opening = Task { await model.loadDetail(id: summary.id) }
        guard await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: detailPath) else {
            CannedFeedURLProtocol.releaseResponse(path: detailPath)
            await opening.value
            return
        }
        XCTAssertTrue(model.isLoadingDetail)
        XCTAssertNoThrow(try ReviewDisputeDetail(viewModel: model, id: summary.id).inspect()
            .find(ViewType.ProgressView.self))
        CannedFeedURLProtocol.releaseResponse(path: detailPath)
        await opening.value
        XCTAssertFalse(model.isLoadingDetail)
        let full = try XCTUnwrap(model.detailReviewDispute)
        XCTAssertFalse(full.claimText.isEmpty)
        XCTAssertNoThrow(try ReviewDisputeDetail(viewModel: model, id: summary.id).inspect().find(button: "Approve"))
    }

    func testDisputeDetailFailureOffersRetryWithoutActionableData() async throws {
        let model = try disputeModel()
        let id = "00000000-0000-7000-8000-000000000201"
        let path = "/api/v1/disputes/\(id)"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        await model.loadDetail(id: id)
        XCTAssertNotNil(model.detailError)
        XCTAssertNil(model.detailReviewDispute)
        let inspection = try ReviewDisputeDetail(viewModel: model, id: id).inspect()
        XCTAssertNoThrow(try inspection.find(button: "Try Again"))
        XCTAssertThrowsError(try inspection.find(button: "Approve"))
    }

    func testAppealSummaryAndDetailFailureRenderErrorWithoutReviewActions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            ApiFixtureLoader.data("native.moderation.appeals.default"), 200
        )
        let model = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
        await model.load()
        let summary = try XCTUnwrap(model.summaryPagination.items.first)
        XCTAssertTrue(model.appeals.isEmpty)
        XCTAssertThrowsError(try ModerationAppealsSurface(viewModel: model).inspect().find(button: "Approve"))
        let path = "/api/v1/appeals/\(summary.id)"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        await model.loadDetail(id: summary.id)
        XCTAssertNotNil(model.detailError)
        XCTAssertNil(model.detailModerationAppeal)
        let failure = try ModerationAppealDetail(viewModel: model, id: summary.id).inspect()
        XCTAssertNoThrow(try failure.find(button: "Try Again"))
        XCTAssertThrowsError(try failure.find(button: "Approve"))
        CannedFeedURLProtocol.handlers[path] = (ApiFixtureLoader.data("native.moderation.appeals.detail.default"), 200)
        await model.loadDetail(id: summary.id)
        XCTAssertNil(model.detailError)
        XCTAssertNotNil(model.detailModerationAppeal?.staffContext)
        XCTAssertNoThrow(try ModerationAppealDetail(viewModel: model, id: summary.id).inspect().find(button: "Approve"))
    }

    func testReportIntegrityLoadsReporterIdsOnlyInDetailAndCanRetryFailure() async throws {
        let model = try ReportIntegrityViewModel(
            service: APIReportIntegrityService(client: makeClient()), viewerTier: .administrator
        )
        let data = ApiFixtureLoader.data("native.moderation.report-integrity.flag")
        let flag = try JSONDecoder.vouchaFixtureDecoder.decode(ReportIntegrityFlagEnvelope.self, from: data).flag
        let path = "/api/v1/report-integrity/flags/\(flag.id)"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        await model.loadDetail(id: flag.id)
        XCTAssertTrue(model.detailFailed)
        XCTAssertNoThrow(try ReportIntegrityDetail(viewModel: model, id: flag.id).inspect().find(button: "Try Again"))
        CannedFeedURLProtocol.handlers[path] = (data, 200)
        await model.loadDetail(id: flag.id)
        XCTAssertFalse(model.detailFailed)
        XCTAssertNotNil(model.detailFlag?.details["reporter_user_ids"])
        let detail = try ReportIntegrityDetail(viewModel: model, id: flag.id).inspect()
        XCTAssertNoThrow(try detail.find(text: "Evidence"))
    }

    private func disputeModel() throws -> ReviewDisputesViewModel {
        try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false
        )
    }
}
