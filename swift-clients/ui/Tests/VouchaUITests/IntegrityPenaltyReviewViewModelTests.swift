import Foundation
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class IntegrityPenaltyReviewViewModelTests: XCTestCase {
    private typealias Support = IntegrityPenaltyReviewTestSupport

    private func makeModel(
        _ domain: IntegrityDomain,
        tier: IntegrityViewerTier = .administrator,
        service: IntegrityPenaltyReviewServiceDouble
    ) -> IntegrityPenaltyReviewViewModel {
        IntegrityPenaltyReviewViewModel(service: service, viewerTier: tier, domain: domain)
    }

    func testLoadsBoundedQueueAndPaginatesForBothDomains() async throws {
        for domain in [IntegrityDomain.report, .vote] {
            let service = IntegrityPenaltyReviewServiceDouble()
            let first = try Support.dueRows(domain, "review-due.page-1")
            let second = try Support.dueRows(domain, "review-due.page-2")
            service.duePages = [.success(first), .success(second), .success(second)]
            let model = makeModel(domain, service: service)

            await model.load()
            XCTAssertEqual(model.penalties.map(\.id), first.results.map(\.id))
            XCTAssertTrue(model.hasMore)
            XCTAssertEqual(service.dueCalls.first?.2, 25)

            await model.loadMore()
            await model.loadMore()
            XCTAssertEqual(model.penalties.count, 2)
            XCTAssertFalse(model.hasMore)
            XCTAssertEqual(service.dueCalls.count, 2)
            XCTAssertEqual(service.dueCalls[1].1, first.pageInfo.endCursor)
        }
    }

    func testConfirmAdoptsServerReviewAndCannotAdvanceTwice() async throws {
        for domain in [IntegrityDomain.report, .vote] {
            let service = IntegrityPenaltyReviewServiceDouble()
            let due = try Support.dueRows(domain)
            let reviewed = try Support.reviewed(domain)
            service.duePages = [.success(due)]
            service.reviewResults = [.success(reviewed)]
            let model = makeModel(domain, service: service)
            await model.load()
            let row = try XCTUnwrap(model.penalties.first)

            await model.confirmReview(row)
            await model.confirmReview(row)

            XCTAssertEqual(service.reviewCalls.count, 1)
            XCTAssertEqual(model.penalties.first?.reviewDueAt, reviewed.reviewDueAt)
            XCTAssertNotNil(model.penalties.first?.reviewedAt)
            XCTAssertFalse(model.canConfirm(row))
        }
    }

    func testStaleRefreshResponseDoesNotUndoCompletedReview() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        let due = try Support.dueRows(.report)
        let reviewed = try Support.reviewed(.report)
        service.duePages = [.success(due), .success(due)]
        service.reviewResults = [.success(reviewed)]
        let model = makeModel(.report, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)
        await model.confirmReview(row)

        await model.load()

        XCTAssertEqual(model.penalties.first?.reviewDueAt, reviewed.reviewDueAt)
        XCTAssertFalse(model.canConfirm(row))
        XCTAssertTrue(model.committedIds.contains(row.id))
    }

    func testReconciliationUsesPreMutationBaselineAfterRefreshDropsRow() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        let due = try Support.dueRows(.vote)
        let empty = IntegrityPenaltyPage(results: [], pageInfo: due.pageInfo)
        service.duePages = [.success(due), .success(empty)]
        service.reviewResults = [.failure(VouchaError.network(URLError(.timedOut)))]
        service.exactResults = try [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(XCTUnwrap(due.results.first))
        ]
        let model = makeModel(.vote, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)
        await model.confirmReview(row)
        await model.load()
        XCTAssertTrue(model.penalties.isEmpty)

        await model.reconcile(row.id)

        XCTAssertFalse(model.committedIds.contains(row.id))
        XCTAssertTrue(model.reconciliationRequiredIds.isEmpty)
        XCTAssertTrue(model.baselines.isEmpty)
    }

    func testStaleRefreshResponseDoesNotRestoreRevokedPenalty() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        let due = try Support.dueRows(.report)
        service.duePages = [.success(due), .success(due)]
        service.revokeResults = try [.success(IntegrityPenaltyTestSupport.revokedReportRow())]
        let model = makeModel(.report, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)
        await model.revoke(row)

        await model.load()

        XCTAssertTrue(model.penalties.isEmpty)
    }

    func testAmbiguousConfirmReconcilesCommittedReviewWithoutResubmitting() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        service.duePages = try [.success(Support.dueRows(.report))]
        service.reviewResults = [.failure(VouchaError.api(statusCode: 503, preconditionCode: nil))]
        service.exactResults = try [.success(Support.reviewed(.report))]
        let model = makeModel(.report, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)

        await model.confirmReview(row)

        XCTAssertEqual(service.reviewCalls.count, 1)
        XCTAssertTrue(model.reconciliationRequiredIds.isEmpty)
        XCTAssertNil(model.errors[row.id])
        XCTAssertTrue(model.committedIds.contains(row.id))
        XCTAssertNotNil(model.penalties.first?.reviewedAt)
    }

    func testAmbiguousConfirmWithUnchangedServerStateAllowsOneRetry() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        let due = try Support.dueRows(.vote)
        service.duePages = [.success(due)]
        service.reviewResults = try [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(Support.reviewed(.vote))
        ]
        service.exactResults = try [
            .failure(VouchaError.network(URLError(.timedOut))),
            .success(XCTUnwrap(due.results.first))
        ]
        let model = makeModel(.vote, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)

        await model.confirmReview(row)
        XCTAssertEqual(model.errors[row.id], .reconciliationFailed)
        XCTAssertFalse(model.canConfirm(row))
        await model.confirmReview(row)
        XCTAssertEqual(service.reviewCalls.count, 1)

        await model.reconcile(row.id)
        XCTAssertNil(model.errors[row.id])
        XCTAssertTrue(model.canConfirm(row))
        await model.confirmReview(row)
        XCTAssertEqual(service.reviewCalls.count, 2)
        XCTAssertTrue(model.committedIds.contains(row.id))
    }

    func testRevokeRemovesPenaltyAndReconcilesRevokedReadback() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        let revoked = try IntegrityPenaltyTestSupport.revokedReportRow()
        service.duePages = try [.success(Support.dueRows(.report))]
        service.revokeResults = [.success(revoked)]
        let model = makeModel(.report, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)

        await model.revoke(row)
        XCTAssertTrue(model.penalties.isEmpty)

        service.duePages = try [.success(Support.dueRows(.report))]
        service.revokeResults = [.failure(VouchaError.api(statusCode: 500, preconditionCode: nil))]
        service.exactResults = [.success(revoked)]
        await model.load()
        await model.revoke(row)
        XCTAssertTrue(model.penalties.isEmpty)
        XCTAssertTrue(model.reconciliationRequiredIds.isEmpty)
    }

    func testPlainFailuresKeepActionsAndAuthorizationLossRemovesThem() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        service.duePages = try [.success(Support.dueRows(.report))]
        service.reviewResults = [
            .failure(VouchaError.api(statusCode: 409, preconditionCode: nil)),
            .failure(VouchaError.api(statusCode: 403, preconditionCode: nil))
        ]
        service.revokeResults = [.failure(VouchaError.api(statusCode: 422, preconditionCode: nil))]
        let model = makeModel(.report, service: service)
        await model.load()
        let row = try XCTUnwrap(model.penalties.first)

        await model.confirmReview(row)
        XCTAssertEqual(model.errors[row.id], .reviewFailed)
        await model.revoke(row)
        XCTAssertEqual(model.errors[row.id], .revokeFailed)
        XCTAssertTrue(model.canConfirm(row))

        await model.confirmReview(row)
        XCTAssertEqual(model.errors[row.id], .permissionDenied)
        XCTAssertFalse(model.canAccess)
        XCTAssertFalse(model.canConfirm(row))
        XCTAssertFalse(model.canRevoke(row))
    }

    func testLoadFailureContinuationFailureAndPermissionGating() async throws {
        let service = IntegrityPenaltyReviewServiceDouble()
        service.duePages = try [
            .failure(VouchaError.unexpected("boom")),
            .success(Support.dueRows(.vote, "review-due.page-1")),
            .failure(VouchaError.unexpected("boom")),
            .failure(VouchaError.api(statusCode: 401, preconditionCode: nil))
        ]
        let model = makeModel(.vote, service: service)
        await model.load()
        XCTAssertEqual(model.initialError, .loadFailed)
        XCTAssertFalse(model.isLoading)

        await model.load()
        XCTAssertNil(model.initialError)
        await model.loadMore()
        XCTAssertEqual(model.continuationError, .loadMoreFailed)
        await model.loadMore()
        XCTAssertEqual(model.continuationError, .permissionDenied)
        XCTAssertFalse(model.canAccess)

        for tier in [IntegrityViewerTier.anonymous, .member, .siteModerator, .customerSupport] {
            let denied = makeModel(.report, tier: tier, service: IntegrityPenaltyReviewServiceDouble())
            await denied.load()
            XCTAssertFalse(denied.canAccess)
            XCTAssertFalse(denied.viewerTier.canReviewDuePenalties(in: .vote))
            XCTAssertTrue(denied.penalties.isEmpty)
        }
        XCTAssertTrue(IntegrityViewerTier.administrator.canReviewDuePenalties(in: .report))
        XCTAssertTrue(IntegrityViewerTier.administrator.canReviewDuePenalties(in: .vote))
    }
}
