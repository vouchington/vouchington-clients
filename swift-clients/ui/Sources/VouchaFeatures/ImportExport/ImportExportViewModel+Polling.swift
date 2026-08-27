import Foundation
import VouchaAPI
import VouchaModels

extension ImportExportViewModel {
    func submitSourceInput(_ input: SourceImportInput, validating rows: [String]?, token: UUID) async throws {
        try Self.validateRequestSize(input)
        if let rows {
            try Self.validateRows(rows)
        }
        resetMonitoringForSubmission()
        let submission = try await service.importSources(input)
        guard operationIsCurrent(token) else { return }
        sourceSummary = submission.import
        batchId = submission.import.id
        results = []
        startMonitoring(batchId: submission.import.id)
    }

    private func resetMonitoringForSubmission() {
        generation += 1
        monitorTask?.cancel()
        monitorTask = nil
        isMonitoring = false
        batchId = nil
        sourceSummary = nil
        results = []
    }

    func startMonitoring(batchId: String) {
        generation += 1
        let currentGeneration = generation
        monitorTask?.cancel()
        isMonitoring = true
        isWorking = true
        monitorTask = Task { [weak self] in
            await self?.monitor(batchId: batchId, generation: currentGeneration)
        }
    }

    private func monitor(batchId: String, generation: Int) async {
        defer {
            if self.generation == generation {
                isMonitoring = false
                isWorking = false
                monitorTask = nil
            }
        }
        while !Task.isCancelled {
            do {
                let status = try await service.sourceStatus(batchId)
                guard generation == self.generation, batchId == self.batchId else { return }
                guard status.import.id == batchId, isNonRegressing(status.import) else {
                    try await delay()
                    continue
                }
                sourceSummary = status.import
                results = status.rows
                errorMessage = nil
                if status.import.pendingRows == 0 || status.import.completedAt != nil {
                    return
                }
                try await delay()
            } catch is CancellationError {
                return
            } catch {
                guard generation == self.generation else { return }
                errorMessage = Self.message(for: error)
                return
            }
        }
    }

    private func isNonRegressing(_ candidate: RssFeedImportSummary) -> Bool {
        guard let current = sourceSummary, current.id == candidate.id else { return false }
        return candidate.completedRows + candidate.failedRows >= current.completedRows + current.failedRows
            && candidate.pendingRows <= current.pendingRows
    }

    static func validateRows(_ rows: [String]) throws {
        guard !rows.isEmpty else { throw ImportExportValidationError.emptyInput }
        guard rows.count <= maximumRows else { throw ImportExportValidationError.tooManyRows }
    }

    static func validateRequestSize(_ body: some Encodable) throws {
        let encoded = try APIClient.makeEncoder().encode(body)
        guard encoded.count <= maximumRequestBytes else { throw ImportExportValidationError.requestTooLarge }
    }

    static func nonemptyLines(_ text: String) -> [String] {
        text.components(separatedBy: .newlines).compactMap {
            let value = $0.trimmingCharacters(in: .whitespacesAndNewlines)
            return value.isEmpty ? nil : value
        }
    }

    static func message(for error: Error) -> String {
        (error as? LocalizedError)?.errorDescription ?? "Import or export failed."
    }
}
