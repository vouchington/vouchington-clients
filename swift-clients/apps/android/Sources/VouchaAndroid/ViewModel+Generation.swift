import Foundation

extension ViewModel {
    func performGeneration(_ operation: @escaping @MainActor () async throws -> String) async throws -> String {
        let previous = activeGenerationTask
        let operationID = UUID()
        activeGenerationID = operationID
        if let previous { _ = try? await previous.value }
        guard activeGenerationID == operationID else { throw CancellationError() }
        try Task.checkCancellation()
        let task = Task {
            try Task.checkCancellation()
            return try await operation()
        }
        activeGenerationTask = task
        defer {
            if activeGenerationID == operationID { activeGenerationTask = nil }
        }
        return try await withTaskCancellationHandler {
            try await task.value
        } onCancel: {
            task.cancel()
        }
    }
}
