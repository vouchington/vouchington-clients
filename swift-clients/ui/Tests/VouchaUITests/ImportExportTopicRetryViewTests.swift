import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class ImportExportTopicRetryViewTests: XCTestCase {
    func testFailedTopicImportPreservesInputAndRenderedRetrySucceeds() async throws {
        let recorder = TopicRetryRecorder()
        let viewModel = ImportExportViewModel(route: .topics, service: recorder.service())
        viewModel.inputText = "Travel\nLocal News"
        var view = ImportExportView(route: .topics, viewModel: viewModel)

        try view.inspect().find(button: "Import topics").tap()
        await recorder.waitForCall(1)
        let firstTask = try XCTUnwrap(viewModel.operationTask)
        await recorder.completeCall(1)
        await firstTask.value

        XCTAssertEqual(viewModel.inputText, "Travel\nLocal News")
        XCTAssertFalse(viewModel.isWorking)
        view = ImportExportView(route: .topics, viewModel: viewModel)
        let alert = try view.inspect().form().alert()
        XCTAssertEqual(try alert.message().text().string(), "Import or export failed.")
        try alert.dismiss()
        XCTAssertFalse(try view.inspect().find(button: "Import topics").isDisabled())

        try view.inspect().find(button: "Import topics").tap()
        await recorder.waitForCall(2)
        let retryTask = try XCTUnwrap(viewModel.operationTask)
        await recorder.completeCall(2)
        await retryTask.value

        XCTAssertEqual(viewModel.inputText, "")
        let completed = ImportExportView(route: .topics, viewModel: viewModel)
        XCTAssertNoThrow(try completed.inspect().find(text: "Followed"))
        XCTAssertNoThrow(try completed.inspect().find(text: "Recommendation created"))
    }

    func testPerRowTopicFailurePreservesInputForCorrection() async {
        let service = ImportExportService(
            importTopics: { names in
                TopicImportResponse(results: [
                    ImportResult(input: names[0], status: .followed),
                    ImportResult(input: names[1], status: .error, error: "Topic not found")
                ])
            },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            downloadExport: { _, _, _ in throw URLError(.badServerResponse) }
        )
        let viewModel = ImportExportViewModel(route: .topics, service: service)
        viewModel.inputText = "Travel\nMissing"

        await viewModel.importText()

        XCTAssertEqual(viewModel.inputText, "Travel\nMissing")
        XCTAssertEqual(viewModel.results.map(\.status), [.followed, .error])
        XCTAssertNil(viewModel.errorMessage)
    }
}

private actor TopicRetryRecorder {
    private var callCount = 0
    private var callWaiters: [Int: [CheckedContinuation<Void, Never>]] = [:]
    private var completions: [Int: CheckedContinuation<Void, Never>] = [:]

    nonisolated func service() -> ImportExportService {
        ImportExportService(
            importTopics: { names in try await self.importTopics(names) },
            importSources: { _ in throw URLError(.badServerResponse) },
            sourceStatus: { _ in throw URLError(.badServerResponse) },
            downloadExport: { _, _, _ in throw URLError(.badServerResponse) }
        )
    }

    func waitForCall(_ call: Int) async {
        guard callCount < call else { return }
        await withCheckedContinuation { callWaiters[call, default: []].append($0) }
    }

    func completeCall(_ call: Int) {
        completions.removeValue(forKey: call)?.resume()
    }

    private func importTopics(_ names: [String]) async throws -> TopicImportResponse {
        callCount += 1
        let call = callCount
        callWaiters.removeValue(forKey: call)?.forEach { $0.resume() }
        await withCheckedContinuation { completions[call] = $0 }
        if call == 1 {
            throw URLError(.cannotConnectToHost)
        }
        return TopicImportResponse(results: [
            ImportResult(input: names[0], status: .followed),
            ImportResult(input: names[1], status: .recommendationCreated)
        ])
    }
}
