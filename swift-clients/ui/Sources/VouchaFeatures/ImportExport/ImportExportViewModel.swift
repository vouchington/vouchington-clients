import Foundation
import Observation
import VouchaModels

public enum ImportExportValidationError: LocalizedError, Equatable {
    case emptyInput, tooManyRows, requestTooLarge, invalidUTF8

    public var errorDescription: String? {
        switch self {
        case .emptyInput: "Enter at least one value."
        case .tooManyRows: "Imports can contain up to 500 rows."
        case .requestTooLarge: "The encoded request must be 2 MiB or smaller."
        case .invalidUTF8: "Choose a UTF-8 text file."
        }
    }
}

struct ImportResultRow: Identifiable, Equatable {
    enum Identity: Hashable {
        case backend(String)
        case fallback(input: String, occurrence: Int)
    }

    let id: Identity
    let result: ImportResult

    static func identify(_ results: [ImportResult]) -> [ImportResultRow] {
        var inputOccurrences: [String: Int] = [:]
        return results.map { result in
            if let id = result.id {
                return ImportResultRow(id: .backend(id), result: result)
            }
            let occurrence = inputOccurrences[result.input, default: 0]
            inputOccurrences[result.input] = occurrence + 1
            return ImportResultRow(id: .fallback(input: result.input, occurrence: occurrence), result: result)
        }
    }
}

@Observable
@MainActor
public final class ImportExportViewModel {
    public nonisolated static let maximumRows = 500
    public nonisolated static let maximumRequestBytes = 2 * 1_024 * 1_024
    public nonisolated static let selectableExportFilters: [SourceFeedType] = [.article, .podcast, .video]

    public let route: ImportExportRoute
    public var inputText = ""
    public var exportFilter: SourceFeedType?
    public var exportFormat: SourceExportFormat = .opml
    public internal(set) var results: [ImportResult] = []
    public internal(set) var sourceSummary: RssFeedImportSummary?
    public internal(set) var batchId: String?
    public internal(set) var isWorking = false
    public internal(set) var isMonitoring = false
    public internal(set) var errorMessage: String?
    public internal(set) var exportURL: URL?

    let service: ImportExportService
    let delay: @Sendable () async throws -> Void
    var fileReader: @Sendable (URL) async throws -> (String, SourceImportFileFormat)
    var monitorTask: Task<Void, Never>?
    var generation = 0
    var operationTask: Task<Void, Never>?
    var operationToken: UUID?

    public init(
        route: ImportExportRoute,
        service: ImportExportService,
        delay: @escaping @Sendable () async throws -> Void = {
            try await Task.sleep(nanoseconds: 2_000_000_000)
        }
    ) {
        self.route = route
        self.service = service
        self.delay = delay
        fileReader = { url in try await NativeImportExportFiles.readUTF8(from: url) }
        exportFilter = route.initialExportFilter
    }

    public var canResumeMonitoring: Bool {
        batchId != nil && !isMonitoring && sourceSummary?.pendingRows != 0
    }

    var resultRows: [ImportResultRow] {
        ImportResultRow.identify(results)
    }

    public func importText() async {
        await runOperation { viewModel, token in
            await viewModel.performTextImport(token: token)
        }
    }

    public func importFileContents(_ text: String, format: SourceImportFileFormat) async {
        await runOperation { viewModel, token in
            await viewModel.performFileImport(text, format: format, token: token)
        }
    }

    public func selectImportFile(_ url: URL) {
        stopMonitoring()
        startOperation(replacingCurrent: true) { viewModel, token in
            await viewModel.performSelectedFileImport(url, token: token)
        }
    }

    public func stopMonitoring() {
        generation += 1
        monitorTask?.cancel()
        monitorTask = nil
        isMonitoring = false
        isWorking = operationTask != nil
    }

    public func resumeMonitoring() {
        guard let batchId else { return }
        startMonitoring(batchId: batchId)
    }

    public func prepareExport() async {
        await runOperation { viewModel, token in
            await viewModel.performExport(token: token)
        }
    }

    public func cancelOperations() {
        cancelActiveOperation()
        stopMonitoring()
        NativeImportExportFiles.remove(exportURL)
        exportURL = nil
        isWorking = false
    }
}

public enum SourceImportFileFormat: Sendable, Equatable { case csv, opml }
