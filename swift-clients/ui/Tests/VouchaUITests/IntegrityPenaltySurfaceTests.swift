import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityPenaltySurfaceTests: XCTestCase {
    func testReportPenaltyCardRendersFieldsFiltersNavigationAndConfirmation() throws {
        let row = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        let service = IntegrityPenaltyServiceDouble()
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: service,
            viewerTier: .administrator,
            domain: .report
        )
        viewModel.penalties = [row]
        var path: String?
        let surface = IntegrityPenaltyLedgerSurface(
            viewModel: viewModel,
            onNavigate: { path = $0 }
        )
        let inspection = try surface.inspect()

        for text in ["Active", "Revoked", "All", "Mass report campaign", row.id] {
            XCTAssertNoThrow(try inspection.find(text: text))
        }
        try inspection.find(button: "User \(row.userId)").tap()
        XCTAssertEqual(path, "/user/\(row.userId)")

        try inspection.find(button: "Revoke").tap()
        XCTAssertTrue(service.revokeCalls.isEmpty)
        let confirmingSurface = IntegrityPenaltyLedgerSurface(
            viewModel: viewModel,
            pendingRevoke: row
        )
        XCTAssertNoThrow(try confirmingSurface.inspect().find(ViewType.VStack.self).confirmationDialog())
    }

    func testVoteRevokedCardRendersMultiplierAndRevocationWithoutAction() throws {
        let row = try XCTUnwrap(IntegrityPenaltyTestSupport.voteRows("revoked").first)
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: nil,
            viewerTier: .administrator,
            domain: .vote
        )
        viewModel.selectedStatus = .revoked
        viewModel.penalties = [row]
        let inspection = try IntegrityPenaltyLedgerSurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "Voting ring"))
        XCTAssertNoThrow(try inspection.find(text: "20%"))
        XCTAssertNoThrow(try inspection.find(text: XCTUnwrap(row.revokedById)))
        XCTAssertThrowsError(try inspection.find(button: "Revoke"))
    }

    func testLedgerRendersLoadingEmptyInitialContinuationAndReconcileStates() throws {
        let row = try XCTUnwrap(IntegrityPenaltyTestSupport.reportRows("active").first)
        let viewModel = IntegrityPenaltyLedgerViewModel(
            service: nil,
            viewerTier: .administrator,
            domain: .report
        )
        viewModel.isLoading = true
        var inspection = try IntegrityPenaltyLedgerSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(text: "Loading integrity records"))

        viewModel.isLoading = false
        inspection = try IntegrityPenaltyLedgerSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(text: "No penalties"))

        viewModel.initialError = .loadFailed
        inspection = try IntegrityPenaltyLedgerSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(button: "Retry"))

        viewModel.initialError = nil
        viewModel.penalties = [row]
        viewModel.hasMore = true
        viewModel.continuationError = .loadMoreFailed
        viewModel.reconciliationRequiredIds = [row.id]
        viewModel.mutationErrors[row.id] = .reconciliationFailed
        inspection = try IntegrityPenaltyLedgerSurface(viewModel: viewModel).inspect()
        XCTAssertNoThrow(try inspection.find(button: "Retry loading more"))
        XCTAssertNoThrow(try inspection.find(button: "Load more"))
        XCTAssertNoThrow(try inspection.find(button: "Reload result"))
    }

    func testVotePenaltyActionStagesLocalizedConfirmation() throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let service = VoteIntegrityServiceDouble()
        let viewModel = VoteIntegrityViewModel(service: service, viewerTier: .administrator)
        viewModel.flags = [flag]
        let surface = VoteIntegritySurface(viewModel: viewModel)
        let inspection = try surface.inspect()

        try inspection.find(button: "Apply vote penalty").tap()

        XCTAssertTrue(service.penaltyCalls.isEmpty)
        let confirmingSurface = VoteIntegritySurface(
            viewModel: viewModel,
            pendingPenaltyFlag: flag
        )
        XCTAssertNoThrow(try confirmingSurface.inspect().find(ViewType.VStack.self).confirmationDialog())
    }

    func testAmbiguousVotePenaltyRendersUncertainAndKeepsPostDisabled() throws {
        let flag = try IntegrityTestSupport.voteFlag()
        let viewModel = VoteIntegrityViewModel(service: nil, viewerTier: .administrator)
        viewModel.flags = [flag]
        viewModel.ambiguousPenaltyFlagIds = [flag.id]
        viewModel.mutationErrorMessages[flag.id] = "ambiguous"
        let inspection = try VoteIntegritySurface(viewModel: viewModel).inspect()

        XCTAssertNoThrow(try inspection.find(text: "The result is uncertain. Reload it before trying again."))
        XCTAssertTrue(try inspection.find(button: "Apply vote penalty").isDisabled())
        XCTAssertThrowsError(try inspection.find(button: "Reload result"))
    }
}
