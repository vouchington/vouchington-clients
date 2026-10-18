import Foundation
@testable import VouchaAndroid

actor DelayedTranscriptRuntime: AndroidAICoreRunning {
    private var isStarted = false
    private var responseWaiter: CheckedContinuation<String, Error>?
    private var startWaiter: (UUID, CheckedContinuation<Void, Error>)?
    private var generationCalls = 0

    func status() async throws -> AndroidModelState {
        .available
    }

    func download() async throws -> AndroidModelState {
        .available
    }

    func generate(prompt _: String) async throws -> String {
        generationCalls += 1
        if generationCalls > 1 { return "Retry answer" }
        isStarted = true
        startWaiter?.1.resume()
        startWaiter = nil
        return try await withCheckedThrowingContinuation { responseWaiter = $0 }
    }

    func waitUntilStarted() async throws {
        let id = UUID()
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                if isStarted {
                    continuation.resume()
                } else {
                    startWaiter = (id, continuation)
                    Task {
                        try? await Task.sleep(for: .seconds(5))
                        await self.timeoutStartWaiter(id)
                    }
                }
            }
        } onCancel: {
            Task { await self.cancelStartWaiter(id) }
        }
    }

    private func timeoutStartWaiter(_ id: UUID) {
        guard let waiter = startWaiter, waiter.0 == id else { return }
        startWaiter = nil
        waiter.1.resume(throwing: DelayedRuntimeError.generationDidNotStart)
    }

    private func cancelStartWaiter(_ id: UUID) {
        guard let waiter = startWaiter, waiter.0 == id else { return }
        startWaiter = nil
        waiter.1.resume(throwing: CancellationError())
    }

    func finish(_ response: String) {
        responseWaiter?.resume(returning: response)
        responseWaiter = nil
    }

    func fail(_ message: String) {
        responseWaiter?.resume(throwing: DelayedRuntimeError.generationFailed(message))
        responseWaiter = nil
    }

    func generationCount() -> Int {
        generationCalls
    }
}

private enum DelayedRuntimeError: Error {
    case generationDidNotStart
    case generationFailed(String)
}
