import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportPaginationTests: XCTestCase {
    func testOpaquePagesPreserveOrderAndReplaceOverlappingIDs() async {
        let recorder = SourcePageRecorder([
            .page(page([row("1", .pending)], cursor: "opaque+/=", terminal: true)),
            .page(page([row("1", .followed), row("2", .error)], terminal: true))
        ])
        let model = makeModel(recorder)
        await model.importText()
        await model.monitorTask?.value
        let cursors = await recorder.cursors
        XCTAssertEqual(cursors, [nil, "opaque+/="])
        XCTAssertEqual(model.results.map(\.id), ["1", "2"])
        XCTAssertEqual(model.results.map(\.status), [.followed, .error])
        XCTAssertFalse(model.canResumeMonitoring)
    }

    func testLaterPageFailureRetriesExactCursorWithoutResubmission() async {
        let recorder = SourcePageRecorder([
            .page(page([row("1", .followed)], cursor: "next", terminal: true)),
            .failure,
            .page(page([row("2", .error)], terminal: true))
        ])
        let model = makeModel(recorder)
        await model.importText()
        await model.monitorTask?.value
        XCTAssertNotNil(model.errorMessage)
        XCTAssertTrue(model.canResumeMonitoring)
        model.resumeMonitoring()
        await model.monitorTask?.value
        let cursors = await recorder.cursors
        let submissions = await recorder.submissions
        XCTAssertEqual(cursors, [nil, "next", "next"])
        XCTAssertEqual(submissions, 1)
        XCTAssertEqual(model.results.map(\.id), ["1", "2"])
        XCTAssertNil(model.errorMessage)
    }

    func testTerminalLaterPageRequiresFreshTerminalTraversal() async {
        let recorder = SourcePageRecorder([
            .page(page([row("1", .pending)], cursor: "next", terminal: false)),
            .page(page([row("2", .followed)], terminal: true)),
            .page(page([row("1", .followed)], cursor: "final", terminal: true)),
            .page(page([row("2", .followed)], terminal: true))
        ])
        let model = makeModel(recorder)
        await model.importText()
        await model.monitorTask?.value
        let cursors = await recorder.cursors
        XCTAssertEqual(cursors, [nil, "next", nil, "final"])
        XCTAssertEqual(model.results.map(\.status), [.followed, .followed])
    }

    func testRepeatedOrMissingCursorFailsWithoutLooping() async {
        for cursor in [nil, "same"] {
            let recorder = SourcePageRecorder([
                .page(page([row("1", .followed)], cursor: "same", terminal: true)),
                .page(page([row("2", .error)], cursor: cursor, terminal: true, hasNext: true))
            ])
            let model = makeModel(recorder)
            await model.importText()
            await model.monitorTask?.value
            XCTAssertNotNil(model.errorMessage)
            let calls = await recorder.cursors
            XCTAssertEqual(calls.count, 2)
            XCTAssertTrue(model.canResumeMonitoring)
        }
    }

    func testCanceledLaterPageCannotPublishIntoReplacementBatch() async {
        let requested = expectation(description: "later page requested")
        let recorder = SourcePageRecorder([
            .page(page([row("1", .followed)], cursor: "next", terminal: true)),
            .held(requested)
        ])
        let model = makeModel(recorder)
        await model.importText()
        let oldTask = model.monitorTask
        await fulfillment(of: [requested], timeout: 2)
        model.cancelOperations()
        model.batchId = "replacement"
        model.results = [row("new", .followed)]
        await recorder.release(page([row("2", .error)], terminal: true))
        let completed = expectation(description: "old request drained")
        Task { await oldTask?.value
            completed.fulfill()
        }
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertEqual(model.batchId, "replacement")
        XCTAssertEqual(model.results.map(\.id), ["new"])
        XCTAssertNil(model.errorMessage)
    }

    func testOnePagePerTickWaitsBeforeRequestingContinuation() async {
        let paused = expectation(description: "first tick paused")
        let gate = SourceTickGate(paused)
        let recorder = SourcePageRecorder([
            .page(page([row("1", .followed)], cursor: "next", terminal: true)),
            .page(page([row("2", .followed)], terminal: true))
        ])
        let model = ImportExportViewModel(
            route: .sources(initialExportFilter: nil), service: recorder.service(), delay: { await gate.pause() }
        )
        model.inputText = "https://example.test/feed"
        await model.importText()
        await fulfillment(of: [paused], timeout: 2)
        let calls = await recorder.cursors
        XCTAssertEqual(calls, [nil])
        XCTAssertTrue(model.isMonitoring)
        await gate.release()
        await model.monitorTask?.value
        XCTAssertEqual(model.results.map(\.id), ["1", "2"])
    }

    func testFinalRefreshFailureRetainsVisibleRowsAndCanRetryTerminalSummary() async {
        let recorder = SourcePageRecorder([
            .page(page([row("1", .pending)], cursor: "first", terminal: false)),
            .page(page([row("2", .followed)], terminal: true)),
            .page(page([row("1", .followed)], cursor: "refresh", terminal: true)),
            .failure,
            .page(page([row("2", .followed)], terminal: true))
        ])
        let model = makeModel(recorder)
        await model.importText()
        await model.monitorTask?.value
        XCTAssertEqual(model.sourceSummary?.pendingRows, 0)
        XCTAssertEqual(model.results.map(\.status), [.pending, .followed])
        XCTAssertTrue(model.canResumeMonitoring)
        model.resumeMonitoring()
        await model.monitorTask?.value
        let calls = await recorder.cursors
        XCTAssertEqual(calls, [nil, "first", nil, "refresh", "refresh"])
        XCTAssertEqual(model.results.map(\.status), [.followed, .followed])
    }

    func testNewTerminalBatchResetsPriorSnapshotCompletionBeforeLaterPageFailure() async {
        let recorder = SourcePageRecorder([
            .page(page([row("1", .followed), row("2", .followed)], terminal: true)),
            .page(page([row("1", .followed)], cursor: "new-next", terminal: true)),
            .failure,
            .page(page([row("2", .error)], terminal: true))
        ], submissionTerminal: true)
        let model = makeModel(recorder)
        await model.importText()
        await model.monitorTask?.value
        XCTAssertFalse(model.canResumeMonitoring)
        model.inputText = "https://example.test/new-feed"
        await model.importText()
        await model.monitorTask?.value
        XCTAssertNotNil(model.errorMessage)
        XCTAssertTrue(model.canResumeMonitoring)
        model.resumeMonitoring()
        await model.monitorTask?.value
        let calls = await recorder.cursors
        let submits = await recorder.submissions
        XCTAssertEqual(calls, [nil, nil, "new-next", "new-next"])
        XCTAssertEqual(submits, 2)
        XCTAssertEqual(model.results.map(\.status), [.followed, .error])
    }

    func testForeignOrRegressingLaterPageDoesNotPublishMixedSnapshot() async {
        for foreign in [true, false] {
            let invalid = RssFeedImportSummary(
                id: foreign ? "foreign" : "batch", totalRows: 2,
                completedRows: 0, failedRows: 0, pendingRows: 2, completedAt: nil,
                createdAt: Date(timeIntervalSince1970: 0)
            )
            let recorder = SourcePageRecorder([
                .page(page([row("1", .followed)], cursor: "next", terminal: true)),
                .page(RssFeedImportStatus(import: invalid, rows: [row("2", .error)]))
            ])
            let model = makeModel(recorder)
            await model.importText()
            await model.monitorTask?.value
            XCTAssertNotNil(model.errorMessage)
            XCTAssertTrue(model.results.isEmpty)
            XCTAssertTrue(model.canResumeMonitoring)
            let calls = await recorder.cursors
            XCTAssertEqual(calls, [nil, "next"])
        }
    }

    private func makeModel(_ recorder: SourcePageRecorder) -> ImportExportViewModel {
        let model = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: {}
        )
        model.inputText = "https://example.test/feed"
        return model
    }
}

private actor SourcePageRecorder {
    enum Step { case page(RssFeedImportStatus), failure, held(XCTestExpectation) }
    private var steps: [Step]
    private let submissionTerminal: Bool
    private var continuation: CheckedContinuation<RssFeedImportStatus, Never>?
    private(set) var cursors: [String?] = []
    private(set) var submissions = 0

    init(_ steps: [Step], submissionTerminal: Bool = false) {
        self.steps = steps
        self.submissionTerminal = submissionTerminal
    }

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            importSources: { _ in await self.submit() },
            sourceStatus: { _, cursor in try await self.status(cursor) },
            downloadExport: { _, _, _ in throw URLError(.badServerResponse) }
        )
    }

    func release(_ page: RssFeedImportStatus) {
        continuation?.resume(returning: page)
        continuation = nil
    }

    private func submit() -> RssFeedImportSubmission {
        submissions += 1
        return RssFeedImportSubmission(import: summary(terminal: submissionTerminal), statusUrl: "/status")
    }

    private func status(_ cursor: String?) async throws -> RssFeedImportStatus {
        cursors.append(cursor)
        guard !steps.isEmpty else { throw URLError(.badServerResponse) }
        switch steps.removeFirst() {
        case let .page(page): return page
        case .failure: throw URLError(.cannotConnectToHost)
        case let .held(requested):
            return await withCheckedContinuation { continuation in
                self.continuation = continuation
                requested.fulfill()
            }
        }
    }
}

private func row(_ id: String, _ status: ImportResultStatus) -> ImportResult {
    ImportResult(id: id, input: "https://example.test/\(id)", status: status)
}

private func summary(terminal: Bool) -> RssFeedImportSummary {
    RssFeedImportSummary(
        id: "batch", totalRows: 2, completedRows: terminal ? 2 : 0, failedRows: 0,
        pendingRows: terminal ? 0 : 2, completedAt: terminal ? Date(timeIntervalSince1970: 1) : nil,
        createdAt: Date(timeIntervalSince1970: 0)
    )
}

private func page(
    _ rows: [ImportResult], cursor: String? = nil, terminal: Bool, hasNext: Bool? = nil
) -> RssFeedImportStatus {
    RssFeedImportStatus(
        import: summary(terminal: terminal), rows: rows,
        pageInfo: .init(hasNextPage: hasNext ?? (cursor != nil), endCursor: cursor)
    )
}

private actor SourceTickGate {
    let paused: XCTestExpectation
    var continuation: CheckedContinuation<Void, Never>?

    init(_ paused: XCTestExpectation) {
        self.paused = paused
    }

    func pause() async {
        await withCheckedContinuation { continuation in
            self.continuation = continuation
            paused.fulfill()
        }
    }

    func release() {
        continuation?.resume()
        continuation = nil
    }
}
