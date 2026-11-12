import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ModerationAppealsExactRunTests: NativeRouteSurfaceViewModelTestCase {
    private typealias Support = ModerationAppealsTestSupport
    private let path = "/api/v1/appeals/appeal-1"
    private let runId = "appeal_resolution_00000000-0000-7000-8000-000000000202"
    private var runPath: String {
        "\(path)/resolution-drafts/\(runId)"
    }

    func testFailedFirstReadRetainsAdmissionAndRefreshUsesOnlyGetAndPreservesEditedDraft() async throws {
        let (model, appeal) = try await setup()
        model.setDraft("Staff edit", for: appeal)
        CannedFeedURLProtocol.errors[runPath] = URLError(.notConnectedToInternet)
        await model.rerunAI(for: appeal)
        XCTAssertFalse(model.canRerun(appeal))
        let pending = try ModerationAppealCard(viewModel: model, appeal: appeal).inspect()
        XCTAssertTrue(try pending.find(button: "Re-run AI").isDisabled())
        CannedFeedURLProtocol.errors.removeValue(forKey: runPath)
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(model.drafts[appeal.id], "Staff edit")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(runPath), 2)
    }

    func testSameTextAndUnchangedTimestampsCompleteOnlyTheAdmittedRun() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.handlers[runPath] = (run("active"), 200)
        await model.rerunAI(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(completedRunId: "newer-run")), 200)
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(completedRunId: runId)), 200)
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(model.appeals.first?.aiPublicResponse, "AI draft")
    }

    func testRepeatedSameTextUsesNewAdmissionIdentity() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        await model.rerunAI(for: appeal)
        let completed = try XCTUnwrap(model.appeals.first)
        let secondRun = "appeal_resolution_00000000-0000-7000-8000-000000000203"
        var admission = try XCTUnwrap(JSONSerialization.jsonObject(with: Support.queued) as? [String: Any])
        admission["run_id"] = secondRun
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = try (
            JSONSerialization.data(withJSONObject: admission), 202
        )
        let secondPath = "\(path)/resolution-drafts/\(secondRun)"
        CannedFeedURLProtocol.handlers[secondPath] = (run("succeeded", id: secondRun), 200)
        await model.rerunAI(for: completed)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(secondPath), 1)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(completedRunId: secondRun)), 200)
        await model.refreshPendingAIRerun(for: completed)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(model.appeals.first?.aiPublicResponse, completed.aiPublicResponse)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 2)
    }

    func testNewerRequestedRunDoesNotHidePersistedAdmittedResult() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            aiPublicResponse: "Exact admitted result", completedRunId: runId, requestedRunId: "newer-run"
        )), 200)
        await model.rerunAI(for: appeal)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(model.appeals.first?.aiPublicResponse, "Exact admitted result")
        XCTAssertEqual(model.appeals.first?.aiDraftRequestedRunId, "newer-run")
    }

    func testMismatchedRunCannotCompleteOrReleaseAdmission() async throws {
        for status in ["succeeded", "failed", "expired"] {
            let (model, appeal) = try await setup()
            CannedFeedURLProtocol.handlers[runPath] = (run(status, id: "other-run"), 200)
            await model.rerunAI(for: appeal)
            XCTAssertNotNil(model.pendingAIReruns[appeal.id], status)
            XCTAssertFalse(model.canRerun(appeal), status)
        }
    }

    func testKnownFailureAndExpiryReleaseAdmissionWithError() async throws {
        for status in ["failed", "expired"] {
            let (model, appeal) = try await setup()
            CannedFeedURLProtocol.handlers[runPath] = (run(status), 200)
            await model.rerunAI(for: appeal)
            XCTAssertNil(model.pendingAIReruns[appeal.id], status)
            XCTAssertTrue(model.canRerun(appeal), status)
            XCTAssertNotNil(model.errorMessage, status)
        }
    }

    func testCancelledReadRetainsAdmissionAndCanRecover() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.errors[runPath] = URLError(.cancelled)
        await model.rerunAI(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        CannedFeedURLProtocol.errors.removeValue(forKey: runPath)
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testWrongAppealResponseCannotApplyDraft() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(id: "other")), 200)
        await model.rerunAI(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(model.appeals.map(\.id), [appeal.id])
    }

    func testOwnerReopenRetainsRunWhileOtherAccountCannotConsumeIt() async throws {
        let store = AppealAIRerunAdmissionStore()
        let (first, appeal) = try await setup(store: store, userId: "staff-1")
        CannedFeedURLProtocol.handlers[runPath] = (run("active"), 200)
        await first.rerunAI(for: appeal)
        let (other, otherAppeal) = try await setup(store: store, userId: "staff-2")
        XCTAssertTrue(other.canRerun(otherAppeal))
        XCTAssertFalse(other.canRefreshAIRerun(otherAppeal))
        let (reopened, reopenedAppeal) = try await setup(store: store, userId: "staff-1")
        XCTAssertFalse(reopened.canRerun(reopenedAppeal))
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        await reopened.refreshPendingAIRerun(for: reopenedAppeal)
        XCTAssertNil(reopened.pendingAIReruns[appeal.id])
    }

    func testAmbiguousPostNeverAdoptsLatestRequestedRunAndTerminalMutationReleases() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.errors["\(path)/resolution-drafts"] = URLError(.networkConnectionLost)
        await model.rerunAI(for: appeal)
        CannedFeedURLProtocol.errors.removeValue(forKey: "\(path)/resolution-drafts")
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(runPath), 0)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(
            approvedAt: "2026-07-01T11:00:00Z"
        )), 200)
        await model.refreshPendingAIRerun(for: appeal)
        XCTAssertNil(model.pendingAIReruns[appeal.id])
        XCTAssertNotNil(model.appeals.first?.approvedAt)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
    }

    func testHeldRunResponseCannotOverwriteDifferentStatusSelection() async throws {
        let (model, appeal) = try await setup()
        CannedFeedURLProtocol.handlers[runPath] = (run("succeeded"), 200)
        CannedFeedURLProtocol.suspendResponse(path: runPath)
        let held = Task { await model.rerunAI(for: appeal) }
        guard await Support.waitForSuspendedCannedFeedResponse(path: runPath) else {
            CannedFeedURLProtocol.releaseResponse(path: runPath)
            await held.value
            return
        }
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (
            Support.staffList([Support.appeal(id: "resolved", status: "resolved")]), 200
        )
        await model.selectStatus(.resolved)
        CannedFeedURLProtocol.releaseResponse(path: runPath)
        await held.value
        XCTAssertEqual(model.summaryPagination.items.map(\.id), ["resolved"])
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
    }

    func testRunRecordingCannotReplaceAnotherAdmissionOrAdoptEmptyIdentity() async throws {
        let (model, appeal) = try await setup()
        let store = model.admissionStore
        let admission = try XCTUnwrap(store.admit(appeal, for: model.admissionOwner))
        XCTAssertNil(store.recordRun("", appealId: appeal.id, owner: model.admissionOwner, token: admission.token))
        XCTAssertNil(store.recordRun(runId, appealId: appeal.id, owner: nil, token: admission.token))
        XCTAssertNil(store.recordRun(runId, appealId: appeal.id, owner: model.admissionOwner, token: UUID()))
        XCTAssertNil(store.admission(appeal.id, for: model.admissionOwner)?.baseline.runId)
        XCTAssertEqual(store.recordRun(
            runId, appealId: appeal.id, owner: model.admissionOwner, token: admission.token
        )?.baseline.runId, runId)
    }

    func testEmptyAdmittedRunRetainsPendingWithoutRunReadOrRedispatch() async throws {
        let (model, appeal) = try await setup()
        var admission = try XCTUnwrap(JSONSerialization.jsonObject(with: Support.queued) as? [String: Any])
        admission["run_id"] = ""
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = try (
            JSONSerialization.data(withJSONObject: admission), 202
        )
        await model.rerunAI(for: appeal)
        await model.rerunAI(for: appeal)
        XCTAssertNotNil(model.pendingAIReruns[appeal.id])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(path)/resolution-drafts"), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(runPath), 0)
        XCTAssertEqual(model.errorMessage, .message(.nativeSwiftReviewDisputesRerunStillProcessing))
    }

    private func setup(
        store: AppealAIRerunAdmissionStore? = nil, userId: String? = nil
    ) async throws -> (ModerationAppealsViewModel, ModerationAppeal) {
        CannedFeedURLProtocol.handlers["/api/v1/appeals"] = (Support.staffList([Support.appeal()]), 200)
        CannedFeedURLProtocol.handlers["\(path)/resolution-drafts"] = (Support.queued, 202)
        let model = try ModerationAppealsViewModel(
            client: makeClient(), isSignedIn: true, isAdministrator: true, isSiteModerator: false,
            currentUserId: userId, admissionStore: store, rerunPollAttempts: 2, rerunPollDelay: {}
        )
        await model.load()
        await Support.openDetails(in: model)
        CannedFeedURLProtocol.handlers[path] = (Support.envelope(Support.appeal(completedRunId: runId)), 200)
        return try (model, XCTUnwrap(model.appeals.first))
    }

    private func run(_ status: String, id: String? = nil) -> Data {
        Data("""
        {"run_id":"\(id ?? runId)","status":"\(status)","ai_drafted_at":null,"latest_lifecycle_change_id":null}
        """.utf8)
    }
}
