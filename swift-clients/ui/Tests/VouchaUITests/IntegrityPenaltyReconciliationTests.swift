@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityPenaltyReconciliationTests: NativeRouteSurfaceViewModelTestCase {
    func testLedgerRejectsStaleFilterResponseAndSerializesContinuation() async throws {
        let active = try IntegrityPenaltyTestSupport.reportRows("active")
        let revoked = try IntegrityPenaltyTestSupport.reportRows("revoked")
        let service = IntegrityPenaltyServiceDouble()
        service.pages = [
            .success(IntegrityPenaltyTestSupport.page(active)),
            .success(IntegrityPenaltyTestSupport.page(revoked, hasMore: true, cursor: "next")),
            .success(IntegrityPenaltyTestSupport.page(active))
        ]
        service.pageDelays = [.milliseconds(60), .zero, .milliseconds(40)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )

        let stale = Task { await viewModel.load() }
        await Task.yield()
        await viewModel.selectStatus(.revoked)
        await stale.value
        XCTAssertEqual(viewModel.penalties.map(\.id), revoked.map(\.id))

        async let first: Void = viewModel.loadMore()
        await Task.yield()
        async let duplicate: Void = viewModel.loadMore()
        _ = await (first, duplicate)
        XCTAssertEqual(service.calls.count, 3)
    }

    func testFailedReconciliationRemainsRetryableWithoutSecondDelete() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.voteRows("active").first)
        let revoked = try IntegrityPenaltyTestSupport.revokedVoteRow()
        let service = IntegrityPenaltyServiceDouble()
        service.revokeResults = [.failure(VouchaError.api(statusCode: 503, preconditionCode: nil))]
        service.exactResults = [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(revoked)
        ]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .vote
        )
        viewModel.selectedStatus = .all
        viewModel.penalties = [active]

        await viewModel.revoke(active)
        XCTAssertTrue(viewModel.reconciliationRequiredIds.contains(active.id))
        XCTAssertFalse(viewModel.canRevoke(active))
        XCTAssertFalse(viewModel.reconcilingPenaltyIds.contains(active.id))

        await viewModel.reconcile(active.id)

        XCTAssertEqual(service.revokeCalls.count, 1)
        XCTAssertEqual(service.exactCalls.count, 2)
        XCTAssertNotNil(viewModel.penalties.first?.revokedAt)
        XCTAssertFalse(viewModel.reconciliationRequiredIds.contains(active.id))
    }

    func testConcurrentManualReconciliationUsesOneExactGet() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        let revoked = try IntegrityPenaltyTestSupport.revokedReportRow()
        let service = IntegrityPenaltyServiceDouble()
        service.exactDelay = .milliseconds(50)
        service.exactResults = [.success(revoked)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )
        viewModel.penalties = [active]
        viewModel.reconciliationRequiredIds = [active.id]

        async let first: Void = viewModel.reconcile(active.id)
        await Task.yield()
        async let second: Void = viewModel.reconcile(active.id)
        _ = await (first, second)

        XCTAssertEqual(service.exactCalls.count, 1)
    }

    func testDecodingFailedRevokeReconcilesWithGetAndNeverRepeatsDelete() async throws {
        let active = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        let revoked = try IntegrityPenaltyTestSupport.revokedReportRow()
        let decodingFailure = VouchaError.decodingFailed(.dataCorrupted(.init(
            codingPath: [],
            debugDescription: "missing body"
        )))
        let service = IntegrityPenaltyServiceDouble()
        service.revokeResults = [.failure(decodingFailure)]
        service.exactResults = [.success(revoked)]
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )
        viewModel.selectedStatus = .all
        viewModel.penalties = [active]

        await viewModel.revoke(active)
        await viewModel.revoke(active)

        XCTAssertEqual(service.revokeCalls.count, 1)
        XCTAssertEqual(service.exactCalls.count, 1)
        XCTAssertNotNil(viewModel.penalties.first?.revokedAt)
    }
}
