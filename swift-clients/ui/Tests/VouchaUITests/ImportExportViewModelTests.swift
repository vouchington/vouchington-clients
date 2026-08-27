import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportViewModelTests: XCTestCase {
    func testTopicTextValidationAndImportOutcomes() async {
        let recorder = ImportExportRecorder()
        let viewModel = ImportExportViewModel(route: .topics, service: recorder.service())
        await viewModel.importText()
        XCTAssertEqual(viewModel.errorMessage, "Enter at least one value.")

        viewModel.inputText = "Travel\nLocal News"
        await viewModel.importText()

        let topicNames = await recorder.topicNames
        XCTAssertEqual(topicNames, ["Travel", "Local News"])
        XCTAssertEqual(viewModel.results.map(\.status), [.followed, .recommendationCreated])
    }

    func testTopicImportUsesExactTransportEncodedSizeBoundary() async throws {
        let recorder = ImportExportRecorder()
        let viewModel = ImportExportViewModel(route: .topics, service: recorder.service())
        let encoder = APIClient.makeEncoder()
        let framingBytes = try encoder.encode(TopicImportBody(names: [""])).count
        let exactName = String(
            repeating: "x",
            count: ImportExportViewModel.maximumRequestBytes - framingBytes
        )
        XCTAssertEqual(
            try encoder.encode(TopicImportBody(names: [exactName])).count,
            ImportExportViewModel.maximumRequestBytes
        )

        viewModel.inputText = exactName
        await viewModel.importText()

        var topicSubmitCount = await recorder.topicSubmitCount
        XCTAssertEqual(topicSubmitCount, 1)
        XCTAssertNil(viewModel.errorMessage)

        viewModel.inputText = exactName + "x"
        await viewModel.importText()

        XCTAssertEqual(viewModel.errorMessage, "The encoded request must be 2 MiB or smaller.")
        topicSubmitCount = await recorder.topicSubmitCount
        XCTAssertEqual(topicSubmitCount, 1)
    }

    func testSourceValidationRejectsEmptyOversizedAndTooManyInputs() async {
        let recorder = ImportExportRecorder()
        let viewModel = ImportExportViewModel(route: .sources(initialExportFilter: nil), service: recorder.service())
        await viewModel.importText()
        XCTAssertEqual(viewModel.errorMessage, "Enter at least one value.")

        for csv in ["", " \t\r\n "] {
            await viewModel.importFileContents(csv, format: .csv)
            XCTAssertEqual(viewModel.errorMessage, "Enter at least one value.")
        }

        viewModel.inputText = Array(repeating: "https://example.test", count: 501).joined(separator: "\n")
        await viewModel.importText()
        XCTAssertEqual(viewModel.errorMessage, "Imports can contain up to 500 rows.")

        await viewModel.importFileContents(String(repeating: "x", count: 2_097_152), format: .csv)
        XCTAssertEqual(viewModel.errorMessage, "The encoded request must be 2 MiB or smaller.")
        let sourceSubmitCount = await recorder.sourceSubmitCount
        XCTAssertEqual(sourceSubmitCount, 0)
    }

    func testFileImportsRejectMoreThanMaximumParsedFeedURLs() async {
        let csvRecorder = ImportExportRecorder()
        let csvViewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: csvRecorder.service()
        )
        let csv = "url\n" + Array(repeating: "https://example.test/feed.xml", count: 501).joined(separator: "\n")
        await csvViewModel.importFileContents(csv, format: .csv)
        let csvSubmitCount = await csvRecorder.sourceSubmitCount
        XCTAssertEqual(csvSubmitCount, 0)
        XCTAssertEqual(csvViewModel.errorMessage, "Imports can contain up to 500 rows.")

        let opmlRecorder = ImportExportRecorder()
        let opmlViewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: opmlRecorder.service()
        )
        let outlines = Array(repeating: #"<outline xmlUrl="https://example.test/feed.xml"/>"#, count: 501)
        await opmlViewModel.importFileContents("<opml>\(outlines.joined())</opml>", format: .opml)
        let opmlSubmitCount = await opmlRecorder.sourceSubmitCount
        XCTAssertEqual(opmlSubmitCount, 0)
        XCTAssertEqual(opmlViewModel.errorMessage, "Imports can contain up to 500 rows.")
    }

    func testFileImportCountsOnlyParsedFeedURLs() async {
        let recorder = ImportExportRecorder()
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service()
        )
        let ignoredRows = Array(repeating: "Ignored,", count: 501).joined(separator: "\n")
        await viewModel.importFileContents(
            "title,url\n\(ignoredRows)\nValid,https://example.test/feed.xml",
            format: .csv
        )
        let sourceSubmitCount = await recorder.sourceSubmitCount
        XCTAssertEqual(sourceSubmitCount, 1)
        viewModel.cancelOperations()
    }

    func testSourceImportPollsSequentiallyToPartialCompletion() async {
        let recorder = ImportExportRecorder(statuses: [.retrying, .partial])
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: .article),
            service: recorder.service(),
            delay: {}
        )
        viewModel.inputText = "https://example.test/feed.xml\nhttps://invalid.example.test/feed.xml"

        await viewModel.importText()
        await waitUntil { !viewModel.isMonitoring }

        let statusCount = await recorder.statusCount
        XCTAssertEqual(statusCount, 2)
        XCTAssertEqual(viewModel.sourceSummary?.failedRows, 1)
        XCTAssertEqual(viewModel.results.map(\.status), [.followed, .error])
    }

    func testFailedNewSourceSubmissionCannotResumeStaleBatch() async {
        let staleSummary = RssFeedImportSummary(
            id: "stale-batch",
            totalRows: 1,
            completedRows: 0,
            failedRows: 0,
            pendingRows: 1,
            completedAt: nil,
            createdAt: Date(timeIntervalSince1970: 0)
        )
        let service = ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            exportTopics: { TopicExportResponse(results: []) },
            importSources: { _ in throw URLError(.cannotConnectToHost) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            exportSources: { _, _ in Data() }
        )
        let viewModel = ImportExportViewModel(route: .sources(initialExportFilter: nil), service: service)
        viewModel.batchId = staleSummary.id
        viewModel.sourceSummary = staleSummary
        viewModel.results = [ImportResult(input: "old", status: .pending)]
        viewModel.inputText = "https://example.test/new-feed.xml"

        await viewModel.importText()

        XCTAssertNil(viewModel.batchId)
        XCTAssertNil(viewModel.sourceSummary)
        XCTAssertTrue(viewModel.results.isEmpty)
        XCTAssertFalse(viewModel.canResumeMonitoring)
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testStopAndResumePollsSameBatchWithoutResubmitting() async {
        let recorder = ImportExportRecorder(statuses: [.retrying, .partial])
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: { try await Task.sleep(nanoseconds: 100_000_000) }
        )
        viewModel.inputText = "https://example.test/feed.xml"
        await viewModel.importText()
        await waitUntil { viewModel.sourceSummary?.pendingRows == 2 }

        viewModel.stopMonitoring()
        XCTAssertTrue(viewModel.canResumeMonitoring)
        viewModel.resumeMonitoring()
        await waitUntil { !viewModel.isMonitoring }

        let sourceSubmitCount = await recorder.sourceSubmitCount
        XCTAssertEqual(sourceSubmitCount, 1)
        XCTAssertEqual(viewModel.batchId, ImportExportRecorder.batchId)
    }

    func testStatusRetryDoesNotResubmitAfterFailure() async {
        let recorder = ImportExportRecorder(statuses: [.failure, .partial])
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: {}
        )
        viewModel.inputText = "https://example.test/feed.xml"
        await viewModel.importText()
        await waitUntil { !viewModel.isMonitoring }
        XCTAssertNotNil(viewModel.errorMessage)

        viewModel.resumeMonitoring()
        await waitUntil { !viewModel.isMonitoring }
        let sourceSubmitCount = await recorder.sourceSubmitCount
        XCTAssertEqual(sourceSubmitCount, 1)
        XCTAssertEqual(viewModel.sourceSummary?.pendingRows, 0)
    }

    func testSupersededAndRegressingSnapshotsAreIgnored() async {
        let recorder = ImportExportRecorder(statuses: [.progressed, .staleTerminal, .partial])
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: {}
        )
        viewModel.inputText = "https://example.test/feed.xml"
        await viewModel.importText()
        await waitUntil { !viewModel.isMonitoring }

        XCTAssertEqual(viewModel.sourceSummary?.completedRows, 1)
        XCTAssertEqual(viewModel.sourceSummary?.failedRows, 1)
    }

    func testMismatchedBatchTerminalSnapshotIsIgnored() async {
        let recorder = ImportExportRecorder(statuses: [.mismatchedTerminal, .partial])
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: {}
        )
        viewModel.inputText = "https://example.test/feed.xml"

        await viewModel.importText()
        await waitUntil { !viewModel.isMonitoring }

        let statusCount = await recorder.statusCount
        XCTAssertEqual(statusCount, 2)
        XCTAssertEqual(viewModel.batchId, ImportExportRecorder.batchId)
        XCTAssertEqual(viewModel.sourceSummary?.failedRows, 1)
    }

    func testExportFiltersExcludeMixedFeeds() {
        XCTAssertEqual(ImportExportViewModel.selectableExportFilters, [.article, .podcast, .video])
    }

    func testRouteDerivesExportFilterWithoutRestrictingImports() {
        XCTAssertEqual(
            ImportExportRoute(path: "/my/news-sources/import-export"),
            .sources(initialExportFilter: .article)
        )
        XCTAssertEqual(ImportExportRoute(path: "/my/podcasts/import-export"), .sources(initialExportFilter: .podcast))
        XCTAssertEqual(ImportExportRoute(path: "/my/channels/import-export"), .sources(initialExportFilter: .video))
        XCTAssertEqual(ImportExportRoute(path: "/my/sources/import-export"), .sources(initialExportFilter: nil))
        XCTAssertEqual(ImportExportRoute(path: "/my/topics/import-export"), .topics)
    }
}

private actor ImportExportRecorder {
    static let batchId = "70000000-0000-7000-8000-000000000001"
    enum Status { case retrying, progressed, partial, mismatchedTerminal, staleTerminal, failure }
    private(set) var topicNames: [String] = []
    private(set) var topicSubmitCount = 0
    private(set) var sourceSubmitCount = 0
    private(set) var statusCount = 0
    private var statuses: [Status]

    init(statuses: [Status] = []) {
        self.statuses = statuses
    }

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { names in await self.recordTopics(names) },
            exportTopics: { TopicExportResponse(results: []) },
            importSources: { input in await self.submit(input) },
            sourceStatus: { id in try await self.status(id) },
            exportSources: { _, _ in Data() }
        )
    }

    private func recordTopics(_ names: [String]) -> TopicImportResponse {
        topicSubmitCount += 1
        topicNames = names
        return TopicImportResponse(results: names.enumerated().map { index, name in
            ImportResult(input: name, status: index == 0 ? .followed : .recommendationCreated)
        })
    }

    private func submit(_: SourceImportInput) -> RssFeedImportSubmission {
        sourceSubmitCount += 1
        return RssFeedImportSubmission(import: summary(completed: 0, failed: 0, pending: 2), statusUrl: "/status")
    }

    private func status(_: String) throws -> RssFeedImportStatus {
        statusCount += 1
        let next = statuses.isEmpty ? .partial : statuses.removeFirst()
        switch next {
        case .failure: throw URLError(.cannotConnectToHost)
        case .retrying:
            return RssFeedImportStatus(import: summary(completed: 0, failed: 0, pending: 2), rows: [
                ImportResult(input: "https://example.test/feed.xml", status: .pending, error: "Retrying")
            ])
        case .progressed:
            return RssFeedImportStatus(import: summary(completed: 1, failed: 0, pending: 1), rows: [
                ImportResult(input: "https://example.test/feed.xml", status: .followed)
            ])
        case .mismatchedTerminal:
            return RssFeedImportStatus(
                import: summary(id: "another-batch", completed: 2, failed: 0, pending: 0),
                rows: []
            )
        case .staleTerminal:
            return RssFeedImportStatus(import: summary(completed: 0, failed: 0, pending: 0), rows: [])
        case .partial:
            return RssFeedImportStatus(import: summary(completed: 1, failed: 1, pending: 0), rows: [
                ImportResult(input: "https://example.test/feed.xml", status: .followed),
                ImportResult(input: "https://invalid.example.test/feed.xml", status: .error, error: "No feed")
            ])
        }
    }

    private func summary(
        id: String = ImportExportRecorder.batchId,
        completed: Int,
        failed: Int,
        pending: Int
    ) -> RssFeedImportSummary {
        RssFeedImportSummary(
            id: id,
            totalRows: 2,
            completedRows: completed,
            failedRows: failed,
            pendingRows: pending,
            completedAt: pending == 0 ? Date() : nil,
            createdAt: Date(timeIntervalSince1970: 0)
        )
    }
}

@MainActor
private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
    for _ in 0 ..< 100 where !condition() {
        await Task.yield()
    }
}
