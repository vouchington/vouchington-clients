import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportFileSelectionRaceTests: XCTestCase {
    func testSecondFileSelectionOwnsSubmissionWhenFirstReaderFinishesLate() async {
        let reader = SupersededFileReader()
        let recorder = SelectedFileSubmissionRecorder()
        let viewModel = ImportExportViewModel(
            route: .sources(initialExportFilter: nil),
            service: recorder.service(),
            delay: {}
        )
        viewModel.fileReader = { url in await reader.read(url) }

        viewModel.selectImportFile(URL(fileURLWithPath: "/tmp/first.csv"))
        await waitForSelectionRace { await reader.firstReadStarted }
        viewModel.selectImportFile(URL(fileURLWithPath: "/tmp/second.csv"))
        await waitForSelectionRace { await recorder.submissionCount == 1 }
        await reader.finishFirstRead()
        await waitForSelectionRace { await MainActor.run { !viewModel.isWorking } }

        let inputs = await recorder.inputs
        XCTAssertEqual(inputs, [.csv("second")])
        XCTAssertFalse(viewModel.isWorking)
    }
}

private actor SupersededFileReader {
    private(set) var firstReadStarted = false
    private var firstContinuation: CheckedContinuation<(String, SourceImportFileFormat), Never>?

    func read(_ url: URL) async -> (String, SourceImportFileFormat) {
        guard url.lastPathComponent == "first.csv" else { return ("second", .csv) }
        firstReadStarted = true
        return await withCheckedContinuation { firstContinuation = $0 }
    }

    func finishFirstRead() {
        firstContinuation?.resume(returning: ("first", .csv))
        firstContinuation = nil
    }
}

private actor SelectedFileSubmissionRecorder {
    private(set) var inputs: [SourceImportInput] = []
    var submissionCount: Int {
        inputs.count
    }

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { _ in TopicImportResponse(results: []) },
            exportTopics: { TopicExportResponse(results: []) },
            importSources: { input in await self.submit(input) },
            sourceStatus: { _ in await self.status() },
            exportSources: { _, _ in Data() }
        )
    }

    private func submit(_ input: SourceImportInput) -> RssFeedImportSubmission {
        inputs.append(input)
        return RssFeedImportSubmission(import: summary(), statusUrl: "/status")
    }

    private func status() -> RssFeedImportStatus {
        RssFeedImportStatus(import: summary(), rows: [])
    }

    private func summary() -> RssFeedImportSummary {
        RssFeedImportSummary(
            id: "70000000-0000-7000-8000-000000000001",
            totalRows: 1,
            completedRows: 1,
            failedRows: 0,
            pendingRows: 0,
            completedAt: Date(timeIntervalSince1970: 1),
            createdAt: Date(timeIntervalSince1970: 0)
        )
    }
}

private func waitForSelectionRace(_ condition: @escaping @Sendable () async -> Bool) async {
    for _ in 0 ..< 100 {
        if await condition() {
            return
        }
        await Task.yield()
    }
}
