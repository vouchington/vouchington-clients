import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesResolutionAmbiguityTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport

    func testEveryResolutionActionMarksNetworkCommitLossAmbiguous() async throws {
        for action in [ReviewDisputeResolutionAction.remove, .annotate, .dismiss] {
            let (viewModel, dispute) = try makeResolutionViewModel(action: action)
            CannedFeedURLProtocol.errors[resolutionPath] = URLError(.networkConnectionLost)

            await viewModel.resolve(dispute, action: action)

            XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
            XCTAssertFalse(viewModel.canResolve(dispute, action: action))
            CannedFeedURLProtocol.errors = [:]
        }
    }

    func testCancelledResolutionRequiresTargetedRefresh() async throws {
        let (viewModel, dispute) = try makeResolutionViewModel(action: .remove)
        CannedFeedURLProtocol.handlers[resolutionPath] = (
            Support.envelope(Support.dispute(
                status: "resolved",
                sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "remove"
            )),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: resolutionPath)

        let resolution = Task { await viewModel.resolve(dispute, action: .remove) }
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: resolutionPath
        )
        XCTAssertTrue(didSuspend)
        resolution.cancel()
        CannedFeedURLProtocol.releaseResponse(path: resolutionPath)
        await resolution.value

        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
        CannedFeedURLProtocol.handlers[detailPath] = (
            Support.envelope(Support.dispute(
                status: "resolved",
                sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "remove"
            )),
            200
        )
        await viewModel.refreshAmbiguousMutation(for: dispute.id)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertTrue(viewModel.disputes.isEmpty)
    }

    func testDefinitiveResolutionRejectionIsNotAmbiguous() async throws {
        let (viewModel, dispute) = try makeResolutionViewModel(action: .dismiss)
        CannedFeedURLProtocol.handlers[resolutionPath] = (
            Data(#"{"message":"Invalid"}"#.utf8),
            400
        )

        await viewModel.resolve(dispute, action: .dismiss)

        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertTrue(viewModel.canResolve(dispute, action: .dismiss))
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testRerunPollingClearsAmbiguityWhenAnotherModeratorApproves() async throws {
        let viewModel = try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: 1,
            rerunPollDelay: {}
        )
        let dispute = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: Support.envelope(Support.dispute(publicResponse: "Ready"))
        ).dispute
        viewModel.disputes = [dispute]
        CannedFeedURLProtocol.handlers["\(detailPath)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.handlers[detailPath] = (
            Support.envelope(Support.dispute(
                publicResponse: "Ready",
                approvedAt: "2026-07-01T11:00:00Z",
                lifecycleChangeId: "change-2"
            )),
            200
        )

        await viewModel.rerunAI(for: dispute)

        let approved = try XCTUnwrap(viewModel.disputes.first)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertTrue(viewModel.canDeliver(approved))
    }

    func testRerunRefreshClearsAmbiguityWhenDisputeResolved() async throws {
        let viewModel = try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: 1,
            rerunPollDelay: {}
        )
        let dispute = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: Support.envelope(Support.dispute())
        ).dispute
        viewModel.disputes = [dispute]
        CannedFeedURLProtocol.handlers["\(detailPath)/resolution-drafts"] = (Support.queued, 202)
        CannedFeedURLProtocol.errors[detailPath] = URLError(.networkConnectionLost)
        await viewModel.rerunAI(for: dispute)

        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertNotNil(viewModel.errorMessage)
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.handlers[detailPath] = (
            Support.envelope(Support.dispute(
                status: "resolved",
                resolvedAt: "2026-07-01T13:00:00Z",
                resolutionAction: "remove",
                lifecycleChangeId: "change-2"
            )),
            200
        )

        await viewModel.refreshAmbiguousMutation(for: dispute.id)

        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertTrue(viewModel.disputes.isEmpty)
        XCTAssertNil(viewModel.errorMessage)
    }

    private var detailPath: String {
        "/api/v1/disputes/dispute-1"
    }

    private var resolutionPath: String {
        "\(detailPath)/resolution"
    }

    private func makeResolutionViewModel(
        action: ReviewDisputeResolutionAction
    ) throws -> (ReviewDisputesViewModel, ReviewDispute) {
        let viewModel = try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false
        )
        let dispute = try JSONDecoder.vouchaFixtureDecoder.decode(
            ReviewDisputeResponse.self,
            from: Support.envelope(Support.dispute(sentAt: "2026-07-01T12:00:00Z"))
        ).dispute
        viewModel.disputes = [dispute]
        if action == .annotate {
            viewModel.setAnnotationDraft("Public annotation", for: dispute)
        }
        return (viewModel, dispute)
    }
}
