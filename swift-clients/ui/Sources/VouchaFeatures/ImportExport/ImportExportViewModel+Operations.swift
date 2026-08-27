import Foundation
import VouchaAPI
import VouchaModels

extension ImportExportViewModel {
    func runOperation(
        _ operation: @escaping @MainActor (ImportExportViewModel, UUID) async -> Void
    ) async {
        guard let task = startOperation(replacingCurrent: false, operation) else { return }
        await task.value
    }

    @discardableResult
    func startOperation(
        replacingCurrent: Bool,
        _ operation: @escaping @MainActor (ImportExportViewModel, UUID) async -> Void
    ) -> Task<Void, Never>? {
        if replacingCurrent {
            cancelActiveOperation()
        }
        guard operationTask == nil else { return nil }
        let token = UUID()
        operationToken = token
        isWorking = true
        let task = Task { [weak self] in
            guard let self else { return }
            await operation(self, token)
            finishOperation(token: token)
        }
        operationTask = task
        return task
    }

    func cancelActiveOperation() {
        operationToken = nil
        operationTask?.cancel()
        operationTask = nil
        isWorking = isMonitoring
    }

    private func finishOperation(token: UUID) {
        guard operationToken == token else { return }
        operationTask = nil
        operationToken = nil
        isWorking = isMonitoring
    }

    func performTextImport(token: UUID) async {
        let rows = Self.nonemptyLines(inputText)
        do {
            try Self.validateRows(rows)
            if route.isTopics {
                try Self.validateRequestSize(TopicImportBody(names: rows))
                let response = try await service.importTopics(rows)
                guard operationIsCurrent(token) else { return }
                results = response.results
                if response.results.count == rows.count,
                   response.results.allSatisfy(\.status.isSuccessful) {
                    inputText = ""
                }
            } else {
                try await submitSourceInput(.urls(rows), validating: rows, token: token)
                guard operationIsCurrent(token) else { return }
                inputText = ""
            }
            guard operationIsCurrent(token) else { return }
            errorMessage = nil
        } catch is CancellationError {
            return
        } catch {
            recordOperationError(error, token: token)
        }
    }

    func performFileImport(_ text: String, format: SourceImportFileFormat, token: UUID) async {
        do {
            let input: SourceImportInput = format == .csv ? .csv(text) : .opml(text)
            let urls = try NativeSourceImportParser.feedURLs(in: text, format: format)
            try await submitSourceInput(input, validating: urls, token: token)
            guard operationIsCurrent(token) else { return }
            errorMessage = nil
        } catch is CancellationError {
            return
        } catch {
            recordOperationError(error, token: token)
        }
    }

    func performSelectedFileImport(_ url: URL, token: UUID) async {
        do {
            let (text, format) = try await fileReader(url)
            guard operationIsCurrent(token) else { return }
            await performFileImport(text, format: format, token: token)
        } catch is CancellationError {
            return
        } catch {
            recordOperationError(error, token: token)
        }
    }

    func performExport(token: UUID) async {
        NativeImportExportFiles.remove(exportURL)
        exportURL = nil
        do {
            let artifact = try await exportArtifact()
            guard operationIsCurrent(token) else { return }
            let url = try await NativeImportExportFiles.writeExport(
                artifact.data,
                filename: artifact.filename,
                replacing: nil
            )
            guard operationIsCurrent(token) else {
                NativeImportExportFiles.remove(url)
                return
            }
            exportURL = url
            errorMessage = nil
        } catch is CancellationError {
            return
        } catch {
            NativeImportExportFiles.remove(exportURL)
            exportURL = nil
            recordOperationError(error, token: token)
        }
    }

    private func exportArtifact() async throws -> (data: Data, filename: String) {
        if route.isTopics {
            let response = try await service.exportTopics()
            let encoder = APIClient.makeEncoder()
            encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
            return try (encoder.encode(response.results), "topics.json")
        }
        let data = try await service.exportSources(exportFilter, exportFormat)
        return (data, exportFormat == .csv ? "rss-feeds.csv" : "rss-feeds.opml")
    }

    func operationIsCurrent(_ token: UUID) -> Bool {
        operationToken == token && !Task.isCancelled
    }

    private func recordOperationError(_ error: Error, token: UUID) {
        guard operationIsCurrent(token) else { return }
        errorMessage = Self.message(for: error)
    }
}

private extension ImportResultStatus {
    var isSuccessful: Bool {
        switch self {
        case .followed, .imported, .sourceCreated, .recommendationCreated, .alreadyFollowing:
            true
        case .pending, .error:
            false
        }
    }
}
