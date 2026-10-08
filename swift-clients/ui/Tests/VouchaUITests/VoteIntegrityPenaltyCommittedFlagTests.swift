@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class VoteIntegrityPenaltyCommittedFlagTests: XCTestCase {
    func testPenaltyAppliesReturnedFlagToEverySelectedStatus() async throws {
        let pending = try IntegrityTestSupport.voteFlag()
        let resolved = try IntegrityTestSupport.voteFlag(resolution: "penalized")
        for status in [IntegrityStatusFilter.pending, .resolved, .all] {
            let service = VoteIntegrityServiceDouble()
            service.penaltyIdResults = [.success([])]
            service.penaltyResults = try [.success(IntegrityTestSupport.votePenalty(count: 3, flag: resolved))]
            let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
            viewModel.selectedStatus = status
            viewModel.flags = [pending]

            await viewModel.applyPenalty(pending)

            XCTAssertEqual(viewModel.penalizedUserCounts[pending.id], 3)
            if status == .pending {
                XCTAssertTrue(viewModel.flags.isEmpty)
            } else {
                XCTAssertEqual(viewModel.flags.first?.resolution, .penalized)
                XCTAssertEqual(viewModel.flags.first?.resolvedById, resolved.resolvedById)
            }
            XCTAssertFalse(viewModel.canApplyPenalty(pending))
        }
    }

    func testAmbiguousCommittedPenaltyFetchesAndEvictsAuthoritativeFlag() async throws {
        let pending = try IntegrityTestSupport.voteFlag()
        let resolved = try IntegrityTestSupport.voteFlag(resolution: "penalized")
        let service = VoteIntegrityServiceDouble()
        service.penaltyResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        service.penaltyIdResults = [.success([]), .success(["committed"])]
        service.exactResults = [.success(resolved)]
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [pending]

        await viewModel.applyPenalty(pending)

        XCTAssertEqual(service.exactCalls, [pending.id])
        XCTAssertTrue(viewModel.flags.isEmpty)
        XCTAssertFalse(viewModel.reconciliationRequiredFlagIds.contains(pending.id))
        XCTAssertEqual(service.penaltyCalls, [pending.id])
    }
}
