import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesConcurrencyTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport

    func testAmbiguousRefreshPreservesUnconfirmedLocalResponseAndClearsConfirmedEdit() async throws {
        let viewModel = try staffViewModel()
        let dispute = try decodeDispute(Support.dispute(publicResponse: "Server response"))
        viewModel.disputes = [dispute]
        viewModel.seedDrafts(from: [dispute])
        viewModel.setPublicResponseDraft("Unsaved response", for: dispute)
        viewModel.ambiguousDisputeIds.insert(dispute.id)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute(publicResponse: "Server response")), 200
        )

        await viewModel.refreshAmbiguousMutation(for: dispute.id)

        let unconfirmed = try XCTUnwrap(viewModel.disputes.first)
        XCTAssertEqual(viewModel.publicDraft(for: unconfirmed), "Unsaved response")
        XCTAssertTrue(viewModel.locallyEditedPublicResponseIds.contains(dispute.id))

        viewModel.ambiguousDisputeIds.insert(dispute.id)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute(publicResponse: "Unsaved response")), 200
        )
        await viewModel.refreshAmbiguousMutation(for: dispute.id)

        let confirmed = try XCTUnwrap(viewModel.disputes.first)
        XCTAssertEqual(viewModel.publicDraft(for: confirmed), "Unsaved response")
        XCTAssertFalse(viewModel.locallyEditedPublicResponseIds.contains(dispute.id))
    }

    func testResolutionOutcomeRejectsOlderPendingListSnapshot() async throws {
        let viewModel = try staffViewModel()
        let dispute = try decodeDispute(Support.dispute(sentAt: "2026-07-01T12:00:00Z"))
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (Support.list([Support.dispute(
            sentAt: "2026-07-01T12:00:00Z"
        )]), 200)
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/disputes")
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution"] = (
            Support.envelope(Support.dispute(
                status: "resolved",
                sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "remove"
            )), 200
        )

        let load = Task { await viewModel.load() }
        let suspended = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/disputes"
        )
        XCTAssertTrue(suspended)
        await viewModel.resolve(dispute, action: .remove)
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/disputes")
        await load.value

        XCTAssertTrue(viewModel.disputes.isEmpty)
        XCTAssertFalse(viewModel.disputePagination.isLoading)
    }

    func testCardDisappearanceCancelsOwnedRerunPolling() async throws {
        let viewModel = try staffViewModel()
        let dispute = try decodeDispute(Support.dispute())
        viewModel.disputes = [dispute]
        viewModel.seedDrafts(from: [dispute])
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute()), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/disputes/dispute-1")
        let inspection = try ReviewDisputeCard(viewModel: viewModel, dispute: dispute).inspect()

        try inspection.find(button: "Re-run AI").tap()
        let suspended = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/disputes/dispute-1"
        )
        XCTAssertTrue(suspended)
        try inspection.vStack().callOnDisappear()
        XCTAssertTrue(viewModel.rerunTasks.isEmpty)
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/disputes/dispute-1")
        for _ in 0 ..< 200 where viewModel.isMutating {
            await Task.yield()
        }

        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertFalse(viewModel.isMutating)
    }

    private func staffViewModel() throws -> ReviewDisputesViewModel {
        try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollDelay: {}
        )
    }

    private func decodeDispute(_ json: String) throws -> ReviewDispute {
        try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: Support.envelope(json)
        ).dispute
    }
}
