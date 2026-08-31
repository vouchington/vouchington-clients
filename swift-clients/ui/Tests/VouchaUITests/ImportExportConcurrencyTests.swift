import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportConcurrencyTests: XCTestCase {
    func testRapidTopicImportTapsSubmitOnceAndCancellationPropagates() async throws {
        let recorder = DelayedImportExportRecorder()
        let viewModel = ImportExportViewModel(route: .topics, service: recorder.service())
        viewModel.inputText = "Travel"

        let first = Task { await viewModel.importText() }
        try await recorder.waitForTopicImportStart()
        let second = Task { await viewModel.importText() }
        await second.value

        let topicImportCount = await recorder.topicImportCount
        XCTAssertEqual(topicImportCount, 1)
        viewModel.cancelOperations()
        await first.value
        await second.value

        let cancelledTopicImportCount = await recorder.cancelledTopicImportCount
        XCTAssertEqual(cancelledTopicImportCount, 1)
        XCTAssertTrue(viewModel.results.isEmpty)
        XCTAssertFalse(viewModel.isWorking)
    }

    func testRapidTopicExportTapsExportOnceAndCancellationPropagates() async throws {
        let recorder = DelayedImportExportRecorder()
        let viewModel = ImportExportViewModel(route: .topics, service: recorder.service())

        let first = Task { await viewModel.prepareExport() }
        try await recorder.waitForTopicExportStart()
        let second = Task { await viewModel.prepareExport() }
        await second.value

        let topicExportCount = await recorder.topicExportCount
        XCTAssertEqual(topicExportCount, 1)
        viewModel.cancelOperations()
        await first.value
        await second.value

        let cancelledTopicExportCount = await recorder.cancelledTopicExportCount
        XCTAssertEqual(cancelledTopicExportCount, 1)
        XCTAssertNil(viewModel.exportURL)
        XCTAssertFalse(viewModel.isWorking)
    }

    func testCancelledLateSourceSubmissionCannotStartPolling() async throws {
        let recorder = DelayedImportExportRecorder(ignoreSourceImportCancellation: true)
        let viewModel = ImportExportViewModel(route: .sources(initialExportFilter: nil), service: recorder.service())
        viewModel.inputText = "https://example.test/feed.xml"

        let submission = Task { await viewModel.importText() }
        try await recorder.waitForSourceImportStart()
        viewModel.cancelOperations()
        await submission.value

        let sourceStatusCount = await recorder.sourceStatusCount
        XCTAssertEqual(sourceStatusCount, 0)
        XCTAssertNil(viewModel.batchId)
        XCTAssertFalse(viewModel.isMonitoring)
    }

    func testRapidSourceURLImportsSubmitOnceAndRenderDisabledControls() async throws {
        let recorder = DelayedImportExportRecorder()
        let route = ImportExportRoute.sources(initialExportFilter: nil)
        let viewModel = ImportExportViewModel(route: route, service: recorder.service())
        viewModel.inputText = "https://example.test/feed.xml"

        let first = Task { await viewModel.importText() }
        try await recorder.waitForSourceImportStart()
        let view = ImportExportView(route: route, viewModel: viewModel)
        XCTAssertTrue(try view.inspect().find(button: "Import URLs").isDisabled())
        XCTAssertTrue(try view.inspect().find(button: "Choose CSV, OPML, or XML").isDisabled())

        let second = Task { await viewModel.importText() }
        await second.value
        let sourceImportCount = await recorder.sourceImportCount
        XCTAssertEqual(sourceImportCount, 1)
        viewModel.cancelOperations()
        await first.value
    }

    func testStartWaitTimesOutWhenImportNeverBegins() async {
        let recorder = DelayedImportExportRecorder()

        do {
            try await recorder.waitForTopicImportStart(timeout: .milliseconds(10))
            XCTFail("Expected the start wait to time out")
        } catch let error as ImportExportStartTimeout {
            XCTAssertEqual(error.operation, "topic import")
        } catch {
            XCTFail("Unexpected error: \(error)")
        }
    }
}

private struct ImportExportStartTimeout: LocalizedError {
    let operation: String
    let timeout: Duration

    var errorDescription: String? {
        "Timed out after \(timeout) waiting for \(operation) to start"
    }
}

private actor DelayedImportExportRecorder {
    private enum StartOperation: String {
        case sourceImport = "source import"
        case topicExport = "topic export"
        case topicImport = "topic import"
    }

    private(set) var topicImportCount = 0
    private(set) var cancelledTopicImportCount = 0
    private(set) var topicExportCount = 0
    private(set) var cancelledTopicExportCount = 0
    private(set) var sourceImportCount = 0
    private(set) var sourceStatusCount = 0
    private let ignoreSourceImportCancellation: Bool

    init(ignoreSourceImportCancellation: Bool = false) {
        self.ignoreSourceImportCancellation = ignoreSourceImportCancellation
    }

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { names in try await self.importTopics(names) },
            importSources: { input in try await self.importSources(input) },
            sourceStatus: { id in await self.sourceStatus(id) },
            downloadExport: { _, _, _ in try await self.downloadTopics() }
        )
    }

    private func importTopics(_ names: [String]) async throws -> TopicImportResponse {
        topicImportCount += 1
        do {
            try await Task.sleep(nanoseconds: 30_000_000_000)
        } catch {
            cancelledTopicImportCount += 1
            throw error
        }
        return TopicImportResponse(results: [ImportResult(input: names[0], status: .followed)])
    }

    func waitForTopicImportStart(timeout: Duration = .seconds(2)) async throws {
        try await waitForStart(.topicImport, timeout: timeout)
    }

    private func downloadTopics() async throws -> URL {
        topicExportCount += 1
        do {
            try await Task.sleep(nanoseconds: 30_000_000_000)
        } catch {
            cancelledTopicExportCount += 1
            throw error
        }
        throw URLError(.badServerResponse)
    }

    func waitForTopicExportStart(timeout: Duration = .seconds(2)) async throws {
        try await waitForStart(.topicExport, timeout: timeout)
    }

    private func importSources(_: SourceImportInput) async throws -> RssFeedImportSubmission {
        sourceImportCount += 1
        do {
            try await Task.sleep(nanoseconds: 30_000_000_000)
        } catch where !ignoreSourceImportCancellation {
            throw error
        } catch {}
        return RssFeedImportSubmission(import: summary(), statusUrl: "/status")
    }

    func waitForSourceImportStart(timeout: Duration = .seconds(2)) async throws {
        try await waitForStart(.sourceImport, timeout: timeout)
    }

    private func startCount(_ operation: StartOperation) -> Int {
        switch operation {
        case .sourceImport:
            sourceImportCount
        case .topicExport:
            topicExportCount
        case .topicImport:
            topicImportCount
        }
    }

    private func waitForStart(_ operation: StartOperation, timeout: Duration) async throws {
        let clock = ContinuousClock()
        let deadline = clock.now.advanced(by: timeout)
        while startCount(operation) == 0 {
            guard clock.now < deadline else {
                throw ImportExportStartTimeout(operation: operation.rawValue, timeout: timeout)
            }
            try await clock.sleep(for: .milliseconds(10))
        }
    }

    private func sourceStatus(_: String) -> RssFeedImportStatus {
        sourceStatusCount += 1
        return RssFeedImportStatus(import: summary(), rows: [])
    }

    private func summary() -> RssFeedImportSummary {
        RssFeedImportSummary(
            id: "70000000-0000-7000-8000-000000000001",
            totalRows: 1,
            completedRows: 0,
            failedRows: 0,
            pendingRows: 1,
            completedAt: nil,
            createdAt: Date(timeIntervalSince1970: 0)
        )
    }
}
