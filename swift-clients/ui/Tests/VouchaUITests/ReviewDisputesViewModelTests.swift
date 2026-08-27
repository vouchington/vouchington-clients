import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesViewModelTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport

    func testLoadsStatusFiltersAndCursorContinuationWithoutDuplicates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()], hasNextPage: true, endCursor: "next"), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(), Support.dispute(id: "dispute-2")]), 200
        )
        await viewModel.loadMore()

        XCTAssertEqual(viewModel.disputes.map(\.id), ["dispute-1", "dispute-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[0].query, "limit=25&status=pending")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs[1].query, "limit=25&status=pending&after=next")

        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(id: "dismissed", status: "dismissed")]), 200
        )
        await viewModel.selectStatus(.dismissed)
        XCTAssertEqual(viewModel.disputes.map(\.id), ["dismissed"])
    }

    func testPublicAndAnnotationDraftsRemainIndependentAndClaimIsImmutable() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)

        viewModel.setPublicResponseDraft("Member response", for: dispute)
        viewModel.setAnnotationDraft("Public annotation", for: dispute)

        XCTAssertEqual(dispute.claimText, "The published rating uses the wrong total.")
        XCTAssertEqual(viewModel.publicResponseDrafts[dispute.id], "Member response")
        XCTAssertEqual(viewModel.annotationDrafts[dispute.id], "Public annotation")
    }

    func testDraftStatePreservesLocalEditsAndReplacesRowsBySelectedStatus() throws {
        let viewModel = try staffViewModel()
        let original = try decodeDispute(Support.dispute())
        viewModel.disputes = [original]
        viewModel.seedDrafts(from: [original])
        viewModel.setPublicResponseDraft("Local edit", for: original)
        viewModel.setAnnotationDraft("Keep annotation", for: original)

        let refreshed = try decodeDispute(Support.dispute(aiPublicResponse: "New AI draft"))
        viewModel.seedDrafts(from: [refreshed])
        XCTAssertEqual(viewModel.publicDraft(for: refreshed), "Local edit")
        XCTAssertEqual(viewModel.annotationDraft(for: refreshed), "Keep annotation")

        viewModel.setPublicResponseDraft("New AI draft", for: refreshed)
        XCTAssertFalse(viewModel.locallyEditedPublicResponseIds.contains(refreshed.id))

        let inserted = try decodeDispute(Support.dispute(id: "dispute-2"))
        viewModel.replace(with: inserted)
        XCTAssertEqual(viewModel.disputes.map(\.id), ["dispute-2", "dispute-1"])

        let resolved = try decodeDispute(Support.dispute(id: "dispute-2", status: "resolved"))
        viewModel.replace(with: resolved)
        XCTAssertEqual(viewModel.disputes.map(\.id), ["dispute-1"])
        XCTAssertEqual(
            viewModel.message(for: ReviewTestError.example),
            .verbatim("Review test failure")
        )
    }

    func testSaveHandlesUnchangedAndChangedPublicResponses() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(publicResponse: "Ready")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)

        let unchangedSaved = await viewModel.savePublicResponse(for: dispute)
        XCTAssertTrue(unchangedSaved)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1"), 0)

        viewModel.setPublicResponseDraft("Revised", for: dispute)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute(publicResponse: "Revised")), 200
        )
        let changedSaved = await viewModel.savePublicResponse(for: dispute)
        XCTAssertTrue(changedSaved)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last, "PATCH")
        let savedDispute = try XCTUnwrap(viewModel.disputes.first)
        XCTAssertEqual(viewModel.publicDraft(for: savedDispute), "Revised")
    }

    func testApproveSavesChangedResponseBeforeApproval() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(publicResponse: "Old")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        viewModel.setPublicResponseDraft("Revised", for: dispute)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute(publicResponse: "Revised")), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/approval"] = (
            Support.envelope(Support.dispute(
                publicResponse: "Revised", approvedAt: "2026-07-01T11:00:00Z"
            )), 200
        )

        await viewModel.approve(dispute)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.suffix(2), ["PATCH", "POST"])
        XCTAssertNotNil(viewModel.disputes.first?.approvedAt)
    }

    func testApproveAndDeliveryAmbiguityRequireTargetedRefresh() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(publicResponse: "Ready")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.errors["/api/v1/disputes/dispute-1/approval"] =
            URLError(.networkConnectionLost)
        try await viewModel.approve(XCTUnwrap(viewModel.disputes.first))
        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains("dispute-1"))

        CannedFeedURLProtocol.errors.removeValue(forKey: "/api/v1/disputes/dispute-1/approval")
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute(
                publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
            )), 200
        )
        await viewModel.refreshAmbiguousMutation(for: "dispute-1")
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains("dispute-1"))

        CannedFeedURLProtocol.errors["/api/v1/disputes/dispute-1/delivery"] =
            URLError(.networkConnectionLost)
        try await viewModel.deliver(XCTUnwrap(viewModel.disputes.first))
        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains("dispute-1"))
    }

    func testApprovedResponseCanBeDeliveredSuccessfully() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(
                publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
            )]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/delivery"] = (
            Support.envelope(Support.dispute(
                publicResponse: "Ready",
                approvedAt: "2026-07-01T11:00:00Z",
                sentAt: "2026-07-01T12:00:00Z"
            )), 200
        )

        await viewModel.deliver(dispute)

        XCTAssertNotNil(viewModel.disputes.first?.sentAt)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
    }

    func testServerFailuresDistinguishAmbiguousFromDefinitiveMutations() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([
                Support.dispute(id: "server", publicResponse: "Ready"),
                Support.dispute(id: "client", publicResponse: "Ready")
            ]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let server = try XCTUnwrap(viewModel.disputes.first { $0.id == "server" })
        let client = try XCTUnwrap(viewModel.disputes.first { $0.id == "client" })
        CannedFeedURLProtocol.handlers["/api/v1/disputes/server/approval"] = (
            Data(#"{"message":"Unavailable"}"#.utf8), 503
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/client/approval"] = (
            Data(#"{"message":"Invalid"}"#.utf8), 400
        )

        await viewModel.approve(server)
        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(server.id))
        await viewModel.approve(client)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(client.id))
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testCancelledApprovalRequiresTargetedRefresh() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(publicResponse: "Ready")]), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/approval"] = (
            Support.envelope(Support.dispute(
                publicResponse: "Ready", approvedAt: "2026-07-01T11:00:00Z"
            )), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/disputes/dispute-1/approval")
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)

        let approval = Task { await viewModel.approve(dispute) }
        let path = "/api/v1/disputes/dispute-1/approval"
        let didSuspend = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(path: path)
        XCTAssertTrue(didSuspend)
        approval.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await approval.value

        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertFalse(viewModel.canApprove(dispute))
    }

    func testResolutionActionsRequireDeliveryAndAnnotationBody() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute(sentAt: "2026-07-01T12:00:00Z")]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        XCTAssertFalse(viewModel.canResolve(dispute, action: .annotate))
        viewModel.setAnnotationDraft("Rating corrected on July 1.", for: dispute)
        XCTAssertTrue(viewModel.canResolve(dispute, action: .annotate))

        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution"] = (
            Support.envelope(Support.dispute(
                status: "resolved", sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z", resolutionAction: "annotate"
            )), 200
        )
        await viewModel.resolve(dispute, action: .annotate)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.last?
            .contains("Rating corrected on July 1.") == true)
        XCTAssertTrue(viewModel.disputes.isEmpty)
    }

    func testRemoveAndDismissUseConfirmedResolutionPathsAndRemoveRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([
                Support.dispute(id: "remove", sentAt: "2026-07-01T12:00:00Z"),
                Support.dispute(id: "dismiss", sentAt: "2026-07-01T12:00:00Z")
            ]), 200
        )
        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/disputes/remove/resolution"] = (
            Support.envelope(Support.dispute(
                id: "remove", status: "resolved", sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z", resolutionAction: "remove"
            )), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dismiss/resolution"] = (
            Support.envelope(Support.dispute(
                id: "dismiss", status: "dismissed", sentAt: "2026-07-01T12:00:00Z",
                resolvedAt: "2026-07-01T13:00:00Z", resolutionAction: "dismiss"
            )), 200
        )

        try await viewModel.resolve(
            XCTUnwrap(viewModel.disputes.first { $0.id == "remove" }),
            action: .remove
        )
        try await viewModel.resolve(
            XCTUnwrap(viewModel.disputes.first { $0.id == "dismiss" }),
            action: .dismiss
        )

        XCTAssertTrue(viewModel.disputes.isEmpty)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.contains {
            $0.contains(#""action":"remove""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap { $0 }.contains {
            $0.contains(#""action":"dismiss""#)
        })
    }

    func testRerunPollingStopsWhenDraftAndLifecycleAdvance() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()]), 200
        )
        let viewModel = try staffViewModel(rerunPollAttempts: 3)
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        viewModel.setPublicResponseDraft("Local edit", for: dispute)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/disputes/dispute-1"] = [
            (
                Support.envelope(Support.dispute(
                    aiPublicResponse: "Fresh AI response",
                    aiDraftedAt: "2026-07-01T10:05:00Z",
                    lifecycleChangeId: "change-2"
                )), 200, 0
            )
        ]

        await viewModel.rerunAI(for: dispute)

        XCTAssertEqual(viewModel.disputes.first?.latestLifecycleChangeId, "change-2")
        XCTAssertEqual(viewModel.disputes.first?.aiPublicResponse, "Fresh AI response")
        XCTAssertEqual(viewModel.publicDraft(for: dispute), "Local edit")
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1"), 1)
    }

    func testRerunPollingIsBoundedAndMutationGuardExcludesConcurrentActions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()]), 200
        )
        let viewModel = try staffViewModel(rerunPollAttempts: 2)
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution-drafts"] = (
            Support.queued, 202
        )
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1"] = (
            Support.envelope(Support.dispute()), 200
        )
        await viewModel.rerunAI(for: dispute)

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1"), 2)
        XCTAssertFalse(viewModel.isMutating)
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.canRerun(dispute))
        XCTAssertTrue(viewModel.ambiguousDisputeIds.contains(dispute.id))
    }

    func testRerunPreEnqueueFailureAllowsRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()]), 200
        )
        let viewModel = try staffViewModel(rerunPollAttempts: 1)
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        CannedFeedURLProtocol.errors["/api/v1/disputes/dispute-1/resolution-drafts"] =
            URLError(.networkConnectionLost)

        await viewModel.rerunAI(for: dispute)
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertTrue(viewModel.canRerun(dispute))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1"), 0)
    }

    func testRerunReportsWhenQueueDeclinesTheRequest() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()]), 200
        )
        let viewModel = try staffViewModel(rerunPollAttempts: 1)
        await viewModel.load()
        let dispute = try XCTUnwrap(viewModel.disputes.first)
        CannedFeedURLProtocol.handlers["/api/v1/disputes/dispute-1/resolution-drafts"] = (
            Data(#"{"queued":false,"rerun_by_id":null}"#.utf8), 202
        )

        await viewModel.rerunAI(for: dispute)

        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertTrue(viewModel.canRerun(dispute))
        XCTAssertFalse(viewModel.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedPathCount("/api/v1/disputes/dispute-1"),
            0
        )
    }

    func testInitialAndNextPageFailuresCanBeRetried() async throws {
        CannedFeedURLProtocol.errors["/api/v1/disputes"] = URLError(.notConnectedToInternet)
        let viewModel = try staffViewModel()
        await viewModel.load()
        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertNotNil(viewModel.disputePagination.lastError)
        XCTAssertFalse(viewModel.isLoading)

        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()], hasNextPage: true, endCursor: "next"), 200
        )
        await viewModel.load()
        XCTAssertNil(viewModel.errorMessage)

        CannedFeedURLProtocol.errors["/api/v1/disputes"] = URLError(.notConnectedToInternet)
        await viewModel.loadMore()
        XCTAssertNotNil(viewModel.loadMoreErrorMessage)
        XCTAssertNotNil(viewModel.disputePagination.lastError)

        await viewModel.selectStatus(.pending)
        XCTAssertEqual(viewModel.selectedStatus, .pending)
    }

    func testCancelledInitialAndNextPageLoadsLeaveConsistentState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            Support.list([Support.dispute()], hasNextPage: true, endCursor: "next"), 200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/disputes")
        let initialViewModel = try staffViewModel()
        let initial = Task { await initialViewModel.load() }
        let didSuspendInitial = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/disputes"
        )
        XCTAssertTrue(didSuspendInitial)
        initial.cancel()
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/disputes")
        await initial.value
        XCTAssertFalse(initialViewModel.isLoading)

        let viewModel = try staffViewModel()
        await viewModel.load()
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/disputes")
        let nextPage = Task { await viewModel.loadMore() }
        let didSuspendNext = await ModerationAppealsTestSupport.waitForSuspendedCannedFeedResponse(
            path: "/api/v1/disputes"
        )
        XCTAssertTrue(didSuspendNext)
        nextPage.cancel()
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/disputes")
        await nextPage.value
        XCTAssertFalse(viewModel.disputePagination.isLoading)
    }

    func testSignedOutAndMemberRolesNeverRequestStaffDisputes() async throws {
        let member = try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: false, isSiteModerator: false
        )
        let signedOut = ReviewDisputesViewModel(
            client: nil, isSignedIn: false, isAdministrator: false, isSiteModerator: false
        )
        await member.load()
        await signedOut.load()
        XCTAssertFalse(member.canAccess)
        XCTAssertFalse(signedOut.canAccess)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    private func staffViewModel(
        rerunPollAttempts: Int = 20
    ) throws -> ReviewDisputesViewModel {
        try ReviewDisputesViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true,
            isSiteModerator: false,
            rerunPollAttempts: rerunPollAttempts,
            rerunPollDelay: {}
        )
    }

    private func decodeDispute(_ json: String) throws -> ReviewDispute {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(
            ReviewDisputeResponse.self,
            from: Support.envelope(json)
        ).dispute
    }
}

private enum ReviewTestError: LocalizedError {
    case example

    var errorDescription: String? {
        "Review test failure"
    }
}
