import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidGenerationCancellationTests: XCTestCase {
    func testAccountTransitionCancelsBothProvidersBeforeNextGeneration() async throws {
        for provider in [AndroidChatProvider.aiCore, .endpoint] {
            let started = expectation(description: "Old generation started")
            let cancelled = expectation(description: "Old generation cancelled")
            let prematureStart = expectation(description: "New generation waits for old completion")
            prematureStart.isInverted = true
            let runtime = CancellableGenerationRuntime(
                started: started, cancelled: cancelled, prematureStart: prematureStart
            )
            let model = ViewModel(
                runtime: runtime,
                secretStore: MemorySecrets(),
                settingsStore: LocalLLMSettingsStore(
                    fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
                    secretStore: InMemoryLocalLLMSecretStore()
                ),
                endpointClient: runtime
            )
            if provider == .endpoint {
                model.endpoint = "https://models.example.test"
                model.endpointModel = "local-model"
                try await model.saveEndpointSettings()
                await model.selectProvider(.endpoint)
            }
            model.modelState = .available
            model.draft = "Old account prompt"
            let oldGeneration = Task { await model.sendOnDevice() }
            await fulfillment(of: [started], timeout: 2)
            model.resetForAccountTransition()
            model.draft = "New account prompt"
            let newGeneration = Task { await model.sendOnDevice() }
            await fulfillment(of: [cancelled], timeout: 2)
            await fulfillment(of: [prematureStart], timeout: 0.05)
            XCTAssertEqual(runtime.requestCount, 1)
            runtime.releaseOldGeneration()
            await oldGeneration.value
            await newGeneration.value
            XCTAssertEqual(runtime.cancellationCount, 1)
            XCTAssertEqual(runtime.requestCount, 2)
            XCTAssertEqual(model.messages.map(\.content), ["New account prompt", "New account answer"])
            XCTAssertFalse(model.isGenerating)
            XCTAssertNil(model.errorMessage)
        }
    }
}

final class CancellableGenerationRuntime: AndroidAICoreRunning, AndroidEndpointGenerating, @unchecked Sendable {
    // All mutable continuation and counter state is protected by lock.
    private let lock = NSLock()
    private let started: XCTestExpectation
    private let cancelled: XCTestExpectation
    private var waiter: CheckedContinuation<String, Error>?
    private var didCancel = false
    private var requests = 0
    private var cancellations = 0
    private var prematureStart: XCTestExpectation?
    private var lastInput: CancellableGenerationInput?

    init(started: XCTestExpectation, cancelled: XCTestExpectation, prematureStart: XCTestExpectation? = nil) {
        self.started = started
        self.cancelled = cancelled
        self.prematureStart = prematureStart
    }

    var requestCount: Int {
        lock.withLock { requests }
    }

    var cancellationCount: Int {
        lock.withLock { cancellations }
    }

    func status() async throws -> AndroidModelState {
        .available
    }

    func download() async throws -> AndroidModelState {
        .available
    }

    var requestInput: CancellableGenerationInput? {
        lock.withLock { lastInput }
    }

    func generate(prompt: String) async throws -> String {
        try await respond(to: .aiCore(prompt))
    }

    private func respond(to input: CancellableGenerationInput) async throws -> String {
        let first = lock.withLock {
            requests += 1
            if requests > 1 {
                lastInput = input
                prematureStart?.fulfill()
            }
            return requests == 1
        }
        guard first else { return "New account answer" }
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                let alreadyCancelled = lock.withLock {
                    if didCancel { return true }
                    waiter = continuation
                    return false
                }
                if alreadyCancelled { continuation.resume(throwing: CancellationError()) }
                started.fulfill()
            }
        } onCancel: {
            self.lock.withLock {
                self.didCancel = true
                self.cancellations += 1
            }
            self.cancelled.fulfill()
        }
    }

    func generateAssistantResponse(
        message: String, history: [LocalLLMChatMessage],
        endpoint: LocalLLMEndpointProfile, apiKey: String?
    ) async throws -> String? {
        try await respond(to: .endpoint(message, history, endpoint, apiKey))
    }

    func releaseOldGeneration() {
        let (continuation, wasCancelled) = lock.withLock {
            defer { waiter = nil
                prematureStart = nil
            }
            return (waiter, didCancel)
        }
        if wasCancelled {
            continuation?.resume(throwing: CancellationError())
        } else {
            continuation?.resume(returning: "Uncancelled old response")
        }
    }
}

enum CancellableGenerationInput {
    case aiCore(String)
    case endpoint(String, [LocalLLMChatMessage], LocalLLMEndpointProfile, String?)
}
