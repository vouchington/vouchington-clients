@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityMutationReconciliationTests: NativeRouteSurfaceViewModelTestCase {
    func testAmbiguousReportDismissReconcilesWithExactGet() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let resolved = try IntegrityTestSupport.reportFlag(resolution: "dismissed")
        let service = ReportIntegrityServiceDouble()
        service.dismissResults = [.failure(serverFailure)]
        service.exactResults = [.success(resolved)]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [pending]

        await viewModel.dismiss(pending)

        XCTAssertEqual(service.dismissCalls, [pending.id])
        XCTAssertEqual(service.exactCalls, [pending.id])
        XCTAssertEqual(viewModel.flags.first?.resolution, .dismissed)
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(pending.id))
    }

    func testFailedReportReconciliationCanRetryWithoutSecondMutation() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let resolved = try IntegrityTestSupport.reportFlag(resolution: "dismissed")
        let service = ReportIntegrityServiceDouble()
        service.dismissResults = [.failure(serverFailure)]
        service.exactResults = [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(resolved)
        ]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [pending]

        await viewModel.dismiss(pending)
        XCTAssertTrue(viewModel.reconciliationRequiredFlagIds.contains(pending.id))
        XCTAssertFalse(viewModel.canResolve(pending))

        await viewModel.reconcile(pending.id)

        XCTAssertEqual(service.dismissCalls, [pending.id])
        XCTAssertEqual(service.exactCalls, [pending.id, pending.id])
        XCTAssertEqual(viewModel.flags.first?.resolution, .dismissed)
    }

    func testAmbiguousVoteResolutionReconcilesWithExactGet() async throws {
        let pending = try IntegrityTestSupport.voteFlag()
        let resolved = try IntegrityTestSupport.voteFlag(resolution: "suspended")
        let service = VoteIntegrityServiceDouble()
        service.resolutionResults = [.failure(serverFailure)]
        service.exactResults = [.success(resolved)]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [pending]

        await viewModel.resolve(pending, as: .suspended)

        XCTAssertEqual(service.resolutionCalls.count, 1)
        XCTAssertEqual(service.exactCalls, [pending.id])
        XCTAssertTrue(service.penaltyIdCalls.isEmpty)
        XCTAssertEqual(viewModel.flags.first?.resolution, .suspended)
    }

    func testConfirmedVotePenaltyPreventsConcurrentAndSequentialRepeats() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyDelay = .milliseconds(50)
        service.penaltyIdResults = [.success([])]
        service.penaltyResults = try [.success(IntegrityTestSupport.votePenalty(count: 2))]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        async let first: Void = viewModel.applyPenalty(flag)
        await waitForPenaltyCall(service)
        async let concurrent: Void = viewModel.applyPenalty(flag)
        _ = await (first, concurrent)
        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.penaltyCalls, [flag.id])
        XCTAssertEqual(service.penaltyIdCalls, [flag.id])
        XCTAssertTrue(viewModel.confirmedPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.canApplyPenalty(flag))
    }

    func testAmbiguousVotePenaltyConfirmsOnlyNewScopedPenalty() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        service.penaltyIdResults = [
            .success(["old-revoked"]),
            .success(["old-revoked", "new-committed"])
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)
        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.penaltyCalls, [flag.id])
        XCTAssertEqual(service.penaltyIdCalls, [flag.id, flag.id])
        XCTAssertTrue(service.exactCalls.isEmpty)
        XCTAssertTrue(viewModel.confirmedPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.canApplyPenalty(flag))
    }

    func testAmbiguousVotePenaltyWithOnlyOldRevokedRowCanRetryPost() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = try [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(IntegrityTestSupport.votePenalty(count: 3))
        ]
        service.penaltyIdResults = [
            .success(["old-revoked"]),
            .success(["old-revoked"]),
            .success(["old-revoked"])
        ]
        service.exactResults = [.success(flag)]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)

        XCTAssertTrue(viewModel.canApplyPenalty(flag))
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(flag.id))
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flag.id])
        XCTAssertNil(viewModel.mutationErrorMessages[flag.id])
        XCTAssertEqual(service.exactCalls, [flag.id])

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.penaltyCalls, [flag.id, flag.id])
        XCTAssertEqual(service.penaltyIdCalls, [flag.id, flag.id, flag.id])
        XCTAssertEqual(service.operationCalls, [
            "penalty-ids:\(flag.id)",
            "apply-penalty:\(flag.id)",
            "penalty-ids:\(flag.id)",
            "flag:\(flag.id)",
            "penalty-ids:\(flag.id)",
            "apply-penalty:\(flag.id)"
        ])
        XCTAssertTrue(viewModel.confirmedPenaltyFlagIds.contains(flag.id))
        XCTAssertEqual(viewModel.penalizedUserCounts[flag.id], 3)
    }

    func testAmbiguousVotePenaltyWithoutNewRowRemovesConcurrentlyResolvedFlag() async throws {
        let pending = try IntegrityTestSupport.voteFlag()
        let resolved = try IntegrityTestSupport.voteFlag(resolution: "dismissed")
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        service.penaltyIdResults = [
            .success(["old-revoked"]),
            .success(["old-revoked"])
        ]
        service.exactResults = [.success(resolved)]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [pending]

        await viewModel.applyPenalty(pending)

        XCTAssertTrue(viewModel.flags.isEmpty)
        XCTAssertEqual(service.operationCalls, [
            "penalty-ids:\(pending.id)",
            "apply-penalty:\(pending.id)",
            "penalty-ids:\(pending.id)",
            "flag:\(pending.id)"
        ])
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(pending.id))
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(pending.id))
        XCTAssertFalse(viewModel.canApplyPenalty(pending))
    }

    func testAmbiguousVotePenaltyExactFlagFailureStaysReconcileOnly() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        service.penaltyIdResults = [
            .success(["old-revoked"]),
            .success(["old-revoked"])
        ]
        service.exactResults = [.failure(VouchaError.network(URLError(.cannotConnectToHost)))]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)
        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.operationCalls, [
            "penalty-ids:\(flag.id)",
            "apply-penalty:\(flag.id)",
            "penalty-ids:\(flag.id)",
            "flag:\(flag.id)"
        ])
        XCTAssertEqual(service.penaltyCalls, [flag.id])
        XCTAssertTrue(viewModel.ambiguousPenaltyFlagIds.contains(flag.id))
        XCTAssertTrue(viewModel.reconciliationRequiredFlagIds.contains(flag.id))
        XCTAssertEqual(viewModel.penaltyBaselineIdsByFlagId[flag.id], ["old-revoked"])
        XCTAssertFalse(viewModel.canApplyPenalty(flag))
    }

    func testVotePenaltyBaselineFailureDoesNotPost() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyIdResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.penaltyIdCalls, [flag.id])
        XCTAssertTrue(service.penaltyCalls.isEmpty)
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(flag.id))
        XCTAssertNil(viewModel.penaltyBaselineIdsByFlagId[flag.id])
        XCTAssertTrue(viewModel.canApplyPenalty(flag))
    }

    func testFailedVotePenaltyConfirmationRetriesGetConcurrentlyWithoutAnotherPost() async throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = [.failure(serverFailure)]
        service.penaltyIdResults = [
            .success(["old-revoked"]),
            .failure(VouchaError.network(URLError(.cannotConnectToHost))),
            .success(["old-revoked", "new-committed"])
        ]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]

        await viewModel.applyPenalty(flag)
        await viewModel.applyPenalty(flag)
        async let first: Void = viewModel.reconcile(flag.id)
        async let concurrent: Void = viewModel.reconcile(flag.id)
        _ = await (first, concurrent)
        await viewModel.applyPenalty(flag)

        XCTAssertEqual(service.penaltyCalls, [flag.id])
        XCTAssertEqual(service.penaltyIdCalls, [flag.id, flag.id, flag.id])
        XCTAssertTrue(service.exactCalls.isEmpty)
        XCTAssertTrue(viewModel.confirmedPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.ambiguousPenaltyFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(flag.id))
        XCTAssertFalse(viewModel.canApplyPenalty(flag))
    }

    func testDecodingFailureIsAmbiguousAndFlagMutationReconciles() async throws {
        let pending = try IntegrityTestSupport.reportFlag()
        let resolved = try IntegrityTestSupport.reportFlag(resolution: "dismissed")
        let decodingFailure = VouchaError.decodingFailed(.dataCorrupted(.init(
            codingPath: [],
            debugDescription: "truncated response"
        )))
        XCTAssertTrue(integrityMutationIsAmbiguous(decodingFailure))
        let service = ReportIntegrityServiceDouble()
        service.dismissResults = [.failure(decodingFailure)]
        service.exactResults = [.success(resolved)]
        let viewModel = ReportIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.selectedStatus = .all
        viewModel.flags = [pending]

        await viewModel.dismiss(pending)

        XCTAssertEqual(service.dismissCalls, [pending.id])
        XCTAssertEqual(service.exactCalls, [pending.id])
        XCTAssertEqual(viewModel.flags.first?.resolution, .dismissed)
    }

    func testRequestTimeoutIsAmbiguousWithoutBroadeningOtherClientErrors() {
        let requestTimeouts: [Error] = [
            VouchaError.api(statusCode: 408, preconditionCode: nil),
            VouchaError.apiMessage(
                statusCode: 408,
                preconditionCode: nil,
                message: "Request timed out."
            )
        ]
        let definiteClientErrors: [Error] = [
            VouchaError.api(statusCode: 400, preconditionCode: nil),
            VouchaError.apiMessage(statusCode: 400, preconditionCode: nil, message: "Bad request."),
            VouchaError.api(statusCode: 409, preconditionCode: nil),
            VouchaError.apiMessage(statusCode: 409, preconditionCode: nil, message: "Conflict.")
        ]

        for error in requestTimeouts {
            XCTAssertTrue(integrityMutationIsAmbiguous(error))
        }
        for error in definiteClientErrors {
            XCTAssertFalse(integrityMutationIsAmbiguous(error))
        }
    }

    func testCapabilityNamesStayIndependent() {
        XCTAssertTrue(IntegrityViewerTier.administrator.canReviewIntegrity)
        XCTAssertTrue(IntegrityViewerTier.administrator.canResolveIntegrityFlag)
        XCTAssertTrue(IntegrityViewerTier.administrator.canApplyIntegrityPenalty)
        XCTAssertTrue(IntegrityViewerTier.administrator.canRevokeIntegrityPenalty)
        XCTAssertFalse(IntegrityViewerTier.siteModerator.canReviewIntegrity)
        XCTAssertFalse(IntegrityViewerTier.siteModerator.canResolveIntegrityFlag)
        XCTAssertFalse(IntegrityViewerTier.siteModerator.canApplyIntegrityPenalty)
        XCTAssertFalse(IntegrityViewerTier.siteModerator.canRevokeIntegrityPenalty)
    }

    private var serverFailure: VouchaError {
        .api(statusCode: 503, preconditionCode: nil)
    }

    private func waitForPenaltyCall(_ service: VoteIntegrityServiceDouble) async {
        for _ in 0 ..< 100 where service.penaltyCalls.isEmpty {
            await Task.yield()
        }
    }
}
