import Foundation
@testable import VouchaFeatures
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

    func testSchemaCompletedRunStopsBeforeTheBoundWithoutAnotherAdmission() async throws {
        let viewModel = try await loadedStaff(reads: 4)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.queuedHandlers[runPath(admittedRunId)] = try [
            (pendingRun, 200, 0),
            (schemaRun(completed: true, judgementId: "00000000-0000-7000-8000-000000000399"), 200, 0)
        ]

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 2)
        XCTAssertNil(viewModel.actionErrors[reportId])
    }

    func testCompletedRunWithoutJudgementIdStopsInsteadOfReadingAgain() async throws {
        let viewModel = try await loadedStaff(reads: 4)
        stubAdmission(ModerationReportsTestFixtures.judgement)
        CannedFeedURLProtocol.queuedHandlers[runPath(admittedRunId)] = try [
            (schemaRun(completed: true, judgementId: nil), 200, 0),
            (pendingRun, 200, 0)
        ]

        await viewModel.perform(.rerunJudgement, reportId: reportId)

        XCTAssertEqual(requestCount(method: "POST", path: admissionPath), 1)
        XCTAssertEqual(requestCount(method: "GET", path: runPath(admittedRunId)), 1)
        XCTAssertNil(viewModel.actionErrors[reportId])
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
        XCTAssertNil(viewModel.actionErrors[reportId])
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
        delay: @escaping @Sendable () async throws -> Void = {}
    ) async throws -> ModerationReportsViewModel {
        CannedFeedURLProtocol.handlers["/api/v1/reports"] = (ModerationReportsTestFixtures.staffFlat, 200)
        let viewModel = try ModerationReportsViewModel(
            client: makeClient(),
            viewerTier: .administrator,
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

    private func schemaRun(completed: Bool, judgementId: String?) throws -> Data {
        var body: [String: Any] = ["completed": completed]
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
