import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class ReviewDisputesExactRunTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ReviewDisputesTestSupport
    private let path = "/api/v1/disputes/dispute-1"
    private let admittedRun = "00000000-0000-7000-8000-000000000211"

    func testStatusKeysMapEveryStatusToExistingDisputeCopy() {
        let keys = [ModerationAIRunStatus.active, .succeeded, .failed, .expired].map(\.disputeDraftKey)
        XCTAssertEqual(keys, [
            .nativeSwiftReviewDisputesRerunStillProcessing, nil,
            .nativeSwiftReviewDisputesRerunFailed, .nativeSwiftReviewDisputesRerunExpired
        ])
    }

    func testRowStatusShowsWithoutAdmissionAndNilShowsNothing() throws {
        let model = try makeModel()
        let failed = try decode(Support.dispute(requestedRunStatus: "failed"))
        XCTAssertEqual(model.aiDraftStatusKey(for: failed), ModerationAIRunStatus.failed.disputeDraftKey)
        let empty = try decode(Support.dispute())
        XCTAssertNil(model.aiDraftStatusKey(for: empty))
    }

    func testStaleRowStatusShowsActiveWhileADifferentRunIsPendingAndMatchingRunShowsItsStatus() async throws {
        let (model, dispute) = try await loaded(attempts: 1)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute()), 200)
        await model.rerunAI(for: dispute)
        XCTAssertNotNil(model.ambiguousAIRerunBaselines[dispute.id])
        let stale = try decode(Support.dispute(requestedRunStatus: "failed"))
        XCTAssertEqual(model.aiDraftStatusKey(for: stale), ModerationAIRunStatus.active.disputeDraftKey)
        let nilStatus = try decode(Support.dispute())
        XCTAssertEqual(model.aiDraftStatusKey(for: nilStatus), ModerationAIRunStatus.active.disputeDraftKey)
        let matching = try decode(Support.dispute(requestedRunId: admittedRun, requestedRunStatus: "failed"))
        XCTAssertEqual(model.aiDraftStatusKey(for: matching), ModerationAIRunStatus.failed.disputeDraftKey)
        let succeeded = try decode(Support.dispute(requestedRunId: admittedRun, requestedRunStatus: "succeeded"))
        XCTAssertNil(model.aiDraftStatusKey(for: succeeded))
        let unnamed = try decode(Support.dispute(requestedRunId: admittedRun))
        XCTAssertNil(model.aiDraftStatusKey(for: unnamed))
    }

    func testAdmissionWithoutRunIdShowsActive() throws {
        let model = try makeModel()
        let row = try decode(Support.dispute(requestedRunStatus: "failed"))
        model.ambiguousAIRerunBaselines[row.id] = AIRerunBaseline(dispute: row)
        XCTAssertEqual(model.aiDraftStatusKey(for: row), ModerationAIRunStatus.active.disputeDraftKey)
    }

    func testFailedOrExpiredExactRunEndsTheAdmissionWithItsOwnStatus() async throws {
        for (status, key) in [
            ("failed", UiMessageKey.nativeSwiftReviewDisputesRerunFailed),
            ("expired", .nativeSwiftReviewDisputesRerunExpired)
        ] {
            CannedFeedURLProtocol.reset()
            let (model, dispute) = try await loaded(attempts: 3)
            CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
                requestedRunId: admittedRun, requestedRunStatus: status
            )), 200)
            await model.rerunAI(for: dispute)
            XCTAssertNil(model.ambiguousAIRerunBaselines[dispute.id], status)
            XCTAssertFalse(model.ambiguousDisputeIds.contains(dispute.id), status)
            XCTAssertEqual(model.errorMessage, .verbatim(UiMessages.string(key, locale: .english)), status)
            let shown = try XCTUnwrap(model.disputes.first)
            XCTAssertEqual(shown.aiDraftRequestedRunStatus?.rawValue, status)
            XCTAssertEqual(model.aiDraftStatusKey(for: shown), ModerationAIRunStatus(rawValue: status)?.disputeDraftKey)
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
            XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        }
    }

    func testDifferentRunFailureDoesNotEndTheAdmissionAndRefreshEndsOnlyTheExactRun() async throws {
        let (model, dispute) = try await loaded(attempts: 1)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
            requestedRunId: "newer-run", requestedRunStatus: "failed"
        )), 200)
        await model.rerunAI(for: dispute)
        XCTAssertNotNil(model.ambiguousAIRerunBaselines[dispute.id])
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
            requestedRunId: admittedRun, requestedRunStatus: "active"
        )), 200)
        await model.refreshAmbiguousMutation(for: dispute.id)
        XCTAssertNotNil(model.ambiguousAIRerunBaselines[dispute.id])
        XCTAssertEqual(model.aiDraftStatusKey(for: model.disputes[0]), ModerationAIRunStatus.active.disputeDraftKey)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
            requestedRunId: admittedRun, requestedRunStatus: "failed"
        )), 200)
        await model.refreshAmbiguousMutation(for: dispute.id)
        XCTAssertNil(model.ambiguousAIRerunBaselines[dispute.id])
        XCTAssertEqual(model.aiDraftStatusKey(for: model.disputes[0]), ModerationAIRunStatus.failed.disputeDraftKey)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testAnEarlierRunsDraftDoesNotCompleteTheQueuedRerun() async throws {
        let (model, dispute) = try await loaded(attempts: 2)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
            aiDraftedAt: "2026-07-01T10:05:00Z", lifecycleChangeId: "change-from-older-run",
            requestedRunId: admittedRun, requestedRunStatus: "active", draftRunId: "older-run"
        )), 200)
        await model.rerunAI(for: dispute)
        XCTAssertTrue(model.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertEqual(model.ambiguousAIRerunBaselines[dispute.id]?.runId, admittedRun)
        XCTAssertFalse(model.canRerun(model.disputes[0]))
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 3)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testTheQueuedRunsOwnDraftCompletesTheRerun() async throws {
        let (model, dispute) = try await loaded(attempts: 2)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.dispute(
            aiDraftedAt: "2026-07-01T10:05:00Z", lifecycleChangeId: "change-from-queued-run",
            requestedRunId: admittedRun, requestedRunStatus: "succeeded", draftRunId: admittedRun
        )), 200)
        await model.rerunAI(for: dispute)
        XCTAssertFalse(model.ambiguousDisputeIds.contains(dispute.id))
        XCTAssertNil(model.ambiguousAIRerunBaselines[dispute.id])
        XCTAssertNil(model.errorMessage)
        XCTAssertEqual(model.disputes.first?.aiDraftRunId, admittedRun)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
    }

    func testBaselineWithoutARunIdKeepsTheDraftChangeRule() throws {
        let row = try decode(Support.dispute(
            aiDraftedAt: "2026-07-01T10:05:00Z", lifecycleChangeId: "change-2", draftRunId: "any-run"
        ))
        let original = try decode(Support.dispute())
        let base = AIRerunBaseline(dispute: original)
        XCTAssertTrue(base.hasAdvanced(in: row))
        var named = base
        named.runId = admittedRun
        XCTAssertFalse(named.hasAdvanced(in: row))
    }

    func testCardRendersTheStatusLabel() async throws {
        let (model, _) = try await loaded(attempts: 1)
        let row = try decode(Support.dispute(requestedRunStatus: "failed"))
        let card = try ReviewDisputeCard(viewModel: model, dispute: row).inspect()
        XCTAssertNoThrow(try card.find(viewWithAccessibilityIdentifier: "review-dispute-ai-run-status-dispute-1"))
        let plain = try ReviewDisputeCard(viewModel: model, dispute: decode(Support.dispute())).inspect()
        XCTAssertThrowsError(try plain.find(viewWithAccessibilityIdentifier: "review-dispute-ai-run-status-dispute-1"))
    }

    private func loaded(attempts: Int) async throws -> (ReviewDisputesViewModel, ReviewDispute) {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (Support.staffList([Support.dispute()]), 200)
        let model = try makeModel(attempts: attempts)
        await model.load()
        await Support.openDetails(in: model)
        let dispute = try XCTUnwrap(model.disputes.first)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        return (model, dispute)
    }

    private func makeModel(attempts: Int = 1) throws -> ReviewDisputesViewModel {
        try ReviewDisputesViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false,
            rerunPollAttempts: attempts, rerunPollDelay: {}
        )
    }

    private func decode(_ json: String) throws -> ReviewDispute {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try decoder.decode(ReviewDisputeResponse.self, from: Support.envelope(json)).dispute
    }
}
