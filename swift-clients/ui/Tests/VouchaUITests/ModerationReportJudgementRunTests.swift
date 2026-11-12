import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class ModerationReportJudgementRunTests: NativeRouteSurfaceViewModelTestCase {
    private let reportId = "019f6559-6b31-7171-b5b1-ccd9d702c45e"
    private let admittedRunId = "00000000-0000-7000-8000-000000000303"

    func testPendingFixtureReadsOnlyTheAdmittedRunUntilTheBound() async throws {
        let viewModel = try await loadedStaff(reads: 3)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 3)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testSucceededRunStopsBeforeTheBoundWithoutAnotherAdmission() async throws {
        let viewModel = try await loadedStaff(reads: 4)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.queuedHandlers[runPath(admittedRunId)] = try [
            (pendingRun, 200, 0),
            (schemaRun(status: "succeeded", judgementId: "00000000-0000-7000-8000-000000000399"), 200, 0)
        ]

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 2)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testFailedRunStopsAndSurfacesLocalizedFailure() async throws {
        try await assertEndedRun(status: "failed", key: .nativeSwiftModerationReportsAiJudgementRunFailed)
    }

    func testExpiredRunStopsAndSurfacesLocalizedExpiry() async throws {
        try await assertEndedRun(status: "expired", key: .nativeSwiftModerationReportsAiJudgementRunExpired)
    }

    private func assertEndedRun(status: String, key: UiMessageKey) async throws {
        let viewModel = try await loadedStaff(reads: 4)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.queuedHandlers[runPath(admittedRunId)] = try [
            (schemaRun(status: status, judgementId: nil), 200, 0),
            (pendingRun, 200, 0)
        ]

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 1)
        XCTAssertEqual(viewModel.actionErrors[reportId], .message(key))
    }

    func testUnqueuedAdmissionDoesNotReadARun() async throws {
        let viewModel = try await loadedStaff(reads: 3)
        try stubAdmission(admission(queued: false, runId: admittedRunId))

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 0)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testEmptyAdmittedRunIdDoesNotReadARun() async throws {
        let viewModel = try await loadedStaff(reads: 3)
        try stubAdmission(admission(queued: true, runId: ""))

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(judgementRunGets, 0)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
    }

    func testRunReadFailureDoesNotAdmitAgain() async throws {
        let viewModel = try await loadedStaff(reads: 3)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (Data("{}".utf8), 500)

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 1)
        XCTAssertNotNil(viewModel.actionErrors[reportId])
    }

    func testCancellationDuringTheReadSpacingDoesNotReadAgain() async throws {
        let viewModel = try await loadedStaff(reads: 3, delay: { throw CancellationError() })
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 1)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testCancelledRunReadDoesNotAdmitAgain() async throws {
        let viewModel = try await loadedStaff(reads: 3)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.errors[runPath(admittedRunId)] = URLError(.cancelled)

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 1)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testFailedFirstReadRetainsAdmissionAndManualRefreshNeverPostsAgain() async throws {
        let viewModel = try await loadedStaff(reads: 1)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (Data("{}".utf8), 500)
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)
        await viewModel.refreshPendingJudgement(reportId)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = try (
            schemaRun(status: "failed", judgementId: nil),
            200
        )
        await viewModel.refreshPendingJudgement(reportId)
        XCTAssertFalse(viewModel.isJudgementPending(reportId))
        XCTAssertEqual(viewModel.actionErrors[reportId], .message(.nativeSwiftModerationReportsAiJudgementRunFailed))
        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 3)
    }

    func testPendingRowRendersRefreshAndDisablesRerun() async throws {
        let viewModel = try await loadedStaff(reads: 1)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        let report = try XCTUnwrap(viewModel.staffReports.first { $0.id == reportId })
        let row = ModerationReportActionButtons(
            report: report,
            viewModel: viewModel,
            onWarning: {},
            onConfirm: { _ in }
        )
        let inspected = try row.inspect()
        XCTAssertTrue(try inspected.find(button: "Re-run judgement").isDisabled())
        XCTAssertNoThrow(try inspected.find(text: "Pending"))
        XCTAssertFalse(try inspected.find(button: "Refresh").isDisabled())
        try inspected.find(button: "Refresh").tap()
        await Task.yield()
    }

    func testMismatchedTerminalRunCannotReleaseAdmission() async throws {
        let viewModel = try await loadedStaff(reads: 1)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        var body = try XCTUnwrap(JSONSerialization.jsonObject(with: schemaRun(
            status: "failed",
            judgementId: nil
        )) as? [String: Any])
        body["run_id"] = "00000000-0000-7000-8000-000000000399"
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = try (JSONSerialization.data(withJSONObject: body), 200)
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testAmbiguousAdmissionFailureBlocksAnotherPost() async throws {
        let viewModel = try await loadedStaff(reads: 1)
        CannedFeedURLProtocol.errors[admissionPath] = URLError(.cancelled)
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        await viewModel.refreshPendingJudgement(reportId)
        let report = try XCTUnwrap(viewModel.staffReports.first { $0.id == reportId })
        let row = ModerationReportActionButtons(
            report: report,
            viewModel: viewModel,
            onWarning: {},
            onConfirm: { _ in }
        )
        XCTAssertTrue(try row.inspect().find(button: "Refresh").isDisabled())
        XCTAssertTrue(try row.inspect().find(button: "Re-run judgement").isDisabled())
        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(judgementRunGets, 0)
    }

    func testReopenedOwnerRetainsExactRunWhileOtherOwnerCannotReadIt() async throws {
        let store = ReportJudgementAdmissionStore()
        let original = try await loadedStaff(reads: 1, userId: "alice", store: store)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)
        await original.perform(.rerunJudgement, reportId: reportId)
        let other = try await loadedStaff(reads: 1, userId: "bob", store: store)
        XCTAssertFalse(other.isJudgementPending(reportId))
        await other.refreshPendingJudgement(reportId)
        let reopened = try await loadedStaff(reads: 1, userId: "alice", store: store)
        XCTAssertTrue(reopened.isJudgementPending(reportId))
        await reopened.perform(.rerunJudgement, reportId: reportId)
        await reopened.refreshPendingJudgement(reportId)
        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 2)
        XCTAssertTrue(original.isJudgementPending(reportId))
        XCTAssertFalse(other.isJudgementPending(reportId))
    }

    func testRemovedItemDoesNotReconcileTerminalResponse() async throws {
        let viewModel = try await loadedStaff(reads: 2, delay: {})
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = (pendingRun, 200)
        await viewModel.perform(.rerunJudgement, reportId: reportId)
        viewModel.staffReports.removeAll()
        CannedFeedURLProtocol.handlers[runPath(admittedRunId)] = try (
            schemaRun(status: "failed", judgementId: nil),
            200
        )
        await viewModel.refreshPendingJudgement(reportId)
        XCTAssertTrue(viewModel.isJudgementPending(reportId))
    }

    func testKnownRejectionReleasesAdmissionWhileTimeoutAndServerErrorRetainIt() async throws {
        for status in [400, 401, 403, 404, 408, 500] {
            let viewModel = try await loadedStaff(reads: 1)
            CannedFeedURLProtocol.handlers[admissionPath] = (Data("{}".utf8), status)
            await viewModel.perform(.rerunJudgement, reportId: reportId)
            XCTAssertEqual(viewModel.isJudgementPending(reportId), status == 408 || status == 500)
        }
    }

    private var admissionPath: String {
        "/api/v1/reports/\(reportId)/judgements"
    }

    private var pendingRun: Data {
        ApiFixtureLoader.data("native.moderation.report-judgement-run.pending")
    }

    private var judgementRunGets: Int {
        CannedFeedURLProtocol.capturedRequests.filter {
            $0.method == "GET" && $0.url.path.contains("/judgements/")
        }.count
    }

    private func runPath(_ runId: String) -> String {
        "/api/v1/reports/\(reportId)/judgements/\(runId)"
    }

    private func loadedStaff(
        reads: Int,
        userId: String? = nil,
        store: ReportJudgementAdmissionStore? = nil,
        delay: @escaping @Sendable () async throws -> Void = {}
    ) async throws -> ModerationReportsViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (ModerationReportsTestFixtures.staffFlat, 200)
        let viewModel = try ModerationReportsViewModel(
            client: makeClient(),
            viewerTier: .administrator,
            currentUserId: userId,
            judgementAdmissions: store,
            judgementRunReads: reads,
            judgementRunDelay: delay
        )
        await viewModel.updateMode(.flat)
        return viewModel
    }

    private func stubAdmission(_ body: Data) {
        CannedFeedURLProtocol.handlers[admissionPath] = (body, 202)
    }

    private func admission(queued: Bool, runId: String) throws -> Data {
        let parsed = try JSONSerialization.jsonObject(with: ModerationReportsTestFixtures.judgement)
        var body = try XCTUnwrap(parsed as? [String: Any])
        body["queued"] = queued
        body["run_id"] = runId
        return try JSONSerialization.data(withJSONObject: body)
    }

    private func schemaRun(status: String, judgementId: String?) throws -> Data {
        var body: [String: Any] = ["status": status, "run_id": admittedRunId]
        if let judgementId {
            body["judgement_id"] = judgementId
        } else {
            body["judgement_id"] = NSNull()
        }
        return try JSONSerialization.data(withJSONObject: body)
    }

    private func requestCount(method: String, path: String) -> Int {
        CannedFeedURLProtocol.capturedRequests.filter {
            $0.method == method && $0.url.path == path
        }.count
    }
}
