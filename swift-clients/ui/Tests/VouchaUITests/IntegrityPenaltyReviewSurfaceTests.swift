import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityPenaltyReviewSurfaceTests: XCTestCase {
    func testRendersDueQueueWithConfirmRevokeAndMoreStatesForBothDomains() throws {
        for domain in [IntegrityDomain.report, .vote] {
            let page = try IntegrityPenaltyReviewTestSupport.dueRows(domain, "review-due.page-1")
            let model = IntegrityPenaltyReviewViewModel(service: nil, viewerTier: .administrator, domain: domain)
            model.penalties = page.results
            model.hasMore = true
            let inspection = try IntegrityPenaltyReviewSection(viewModel: model).inspect()

            for text in [
                "Annual penalty reviews", "Review due",
                "Active penalties remain in place after review until staff revokes them.",
                "More penalties are due. Review this page, then refresh."
            ] {
                XCTAssertNoThrow(try inspection.find(text: text))
            }
            for button in ["Refresh", "Confirm continued penalty", "Revoke", "Load more"] {
                XCTAssertNoThrow(try inspection.find(button: button))
            }
        }
    }

    func testRendersEmptyLoadFailedAndPermissionStates() throws {
        let model = IntegrityPenaltyReviewViewModel(service: nil, viewerTier: .administrator, domain: .report)
        XCTAssertNoThrow(try IntegrityPenaltyReviewSection(viewModel: model).inspect()
            .find(text: "No penalties are due for review."))

        model.initialError = .loadFailed
        XCTAssertNoThrow(try IntegrityPenaltyReviewSection(viewModel: model).inspect()
            .find(text: "Could not load reviews due. Refresh to try again."))

        let denied = IntegrityPenaltyReviewViewModel(service: nil, viewerTier: .member, domain: .vote)
        let inspection = try IntegrityPenaltyReviewSection(viewModel: denied).inspect()
        XCTAssertThrowsError(try inspection.find(button: "Confirm continued penalty"))
        XCTAssertThrowsError(try inspection.find(text: "Annual penalty reviews"))

        denied.permissionLost = true
        XCTAssertNoThrow(try IntegrityPenaltyReviewSection(viewModel: denied).inspect()
            .find(text: "Administrator required"))
    }

    func testCommittedRowHidesConfirmAndUncertainRowOffersReload() throws {
        let page = try IntegrityPenaltyReviewTestSupport.dueRows(.report)
        let model = IntegrityPenaltyReviewViewModel(service: nil, viewerTier: .administrator, domain: .report)
        model.penalties = page.results
        let id = try XCTUnwrap(page.results.first?.id)
        model.committedIds = [id]
        var inspection = try IntegrityPenaltyReviewSection(viewModel: model).inspect()
        XCTAssertThrowsError(try inspection.find(button: "Confirm continued penalty"))

        model.committedIds = []
        model.reconciliationRequiredIds = [id]
        model.errors[id] = .resultUncertain
        inspection = try IntegrityPenaltyReviewSection(viewModel: model).inspect()
        XCTAssertNoThrow(try inspection.find(button: "Reload result"))
        XCTAssertThrowsError(try inspection.find(button: "Confirm continued penalty"))
    }
}
