import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AccountTransitionTests: XCTestCase {
    func testAccountTransitionClearsHistoryAndDiscardsPendingGeneration() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        AndroidTranscriptTestFactory.enqueueEmptyConversationCreation(on: fixture.recorder)
        let runtime = BlockingAICoreRuntime()
        let viewModel = ViewModel(
            runtime: runtime,
            secretStore: TransitionTestEndpointSecrets(),
            settingsStore: LocalLLMSettingsStore(
                fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
                secretStore: InMemoryLocalLLMSecretStore()
            ),
            endpointClient: TransitionTestEndpointClient()
        )
        viewModel.modelState = .available
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.messages = [.init(role: "assistant", content: "Prior account response")]
        viewModel.draft = "Private prompt"

        let generation = Task { await viewModel.sendOnDevice() }
        try await runtime.waitUntilStarted()
        viewModel.resetForAccountTransition()
        await runtime.finish("Prior account answer")
        await generation.value

        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertEqual(viewModel.draft, "")
        XCTAssertFalse(viewModel.isGenerating)
        XCTAssertNil(viewModel.errorMessage)
    }
}

private actor BlockingAICoreRuntime: AndroidAICoreRunning {
    private var started = false
    private var responseWaiter: CheckedContinuation<String, Never>?
    private var startWaiter: (UUID, CheckedContinuation<Void, Error>)?

    func status() async throws -> AndroidModelState {
        .available
    }

    func download() async throws -> AndroidModelState {
        .available
    }

    func generate(prompt _: String) async throws -> String {
        started = true
        startWaiter?.1.resume()
        startWaiter = nil
        return await withCheckedContinuation { responseWaiter = $0 }
    }

    func waitUntilStarted() async throws {
        let id = UUID()
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                if started {
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
        waiter.1.resume(throwing: WaitError.generationDidNotStart)
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
}

private enum WaitError: Error {
    case generationDidNotStart
}

private final class TransitionTestEndpointSecrets: AndroidEndpointSecretStoring {
    func string(forKey _: String) throws -> String? {
        nil
    }

    func set(_: String, forKey _: String) throws {}
    func removeValue(forKey _: String) throws {}
}

private actor TransitionTestEndpointClient: AndroidEndpointGenerating {
    func generateAssistantResponse(
        message _: String,
        history _: [LocalLLMChatMessage],
        endpoint _: LocalLLMEndpointProfile,
        apiKey _: String?
    ) async throws -> String? {
        nil
    }
}
