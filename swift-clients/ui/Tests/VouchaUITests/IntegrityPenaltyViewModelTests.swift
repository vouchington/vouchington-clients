@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class IntegrityPenaltyViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testReportLedgerPaginatesDeduplicatesAndPreservesRowsAfterFailure() async throws {
        let first = try IntegrityPenaltyTestSupport.reportRows("default")
        let second = try IntegrityPenaltyTestSupport.reportRows("page-2")
        let service = IntegrityPenaltyServiceDouble()
        service.pages = [
            .success(IntegrityPenaltyTestSupport.page(first, hasMore: true, cursor: "opaque+cursor")),
            .failure(VouchaError.unexpected("continuation failed")),
            .success(IntegrityPenaltyTestSupport.page([first[0], second[0]]))
        ]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )

        await viewModel.load()
        await viewModel.loadMore()
        XCTAssertEqual(viewModel.penalties.map(\.id), first.map(\.id))
        XCTAssertEqual(viewModel.continuationError, .loadMoreFailed)

        await viewModel.loadMore()
        XCTAssertEqual(Set(viewModel.penalties.map(\.id)), Set([first[0].id, second[0].id]))
        XCTAssertEqual(service.calls[1].2, "opaque+cursor")
        XCTAssertEqual(service.calls[1].3, 25)
    }

    func testVoteLedgerFailsClosedWhenServerDoesNotConfirmFlagScope() async {
        let service = IntegrityPenaltyServiceDouble()
        service.pages = [.failure(IntegrityPenaltyServiceError.unconfirmedVoteScope)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .vote
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.initialError, .scopeUnavailable)
        XCTAssertTrue(viewModel.penalties.isEmpty)
    }

    func testRevokeUsesAuthoritativeResponseAndPreventsRepeat() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        let revoked = try IntegrityPenaltyTestSupport.revokedReportRow()
        let service = IntegrityPenaltyServiceDouble()
        service.revokeResults = [.success(revoked)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )
        viewModel.penalties = [active]

        await viewModel.revoke(active)
        await viewModel.revoke(active)

        XCTAssertTrue(viewModel.penalties.isEmpty)
        XCTAssertEqual(service.revokeCalls.count, 1)
    }

    func testAmbiguousRevokeReconcilesExactPenaltyBeforeUnlocking() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.voteRows("active").first)
        let revoked = try IntegrityPenaltyTestSupport.revokedVoteRow()
        let service = IntegrityPenaltyServiceDouble()
        service.revokeResults = [.failure(VouchaError.api(statusCode: 503, preconditionCode: nil))]
        service.exactResults = [.success(revoked)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .vote
        )
        viewModel.selectedStatus = .all
        viewModel.penalties = [active]

        await viewModel.revoke(active)

        XCTAssertEqual(service.revokeCalls.count, 1)
        XCTAssertEqual(service.exactCalls.count, 1)
        XCTAssertNotNil(viewModel.penalties.first?.revokedAt)
        XCTAssertFalse(viewModel.mutatingPenaltyIds.contains(active.id))
        XCTAssertFalse(viewModel.reconciliationRequiredIds.contains(active.id))
    }

    func testOnlyAdministratorsCanLoadOrRevokePenalties() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        for tier in [
            IntegrityViewerTier.anonymous,
            .member,
            .siteModerator,
            .customerSupport
        ] {
            let service = IntegrityPenaltyServiceDouble()
            let viewModel = IntegrityPenaltyLedgerViewModel(service: service, viewerTier: tier, domain: .report)
            viewModel.penalties = [active]
            await viewModel.load()
            await viewModel.revoke(active)
            XCTAssertTrue(service.calls.isEmpty)
            XCTAssertTrue(service.revokeCalls.isEmpty)
        }
    }
}
