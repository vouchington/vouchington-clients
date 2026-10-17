import Foundation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AccountTransitionTests: XCTestCase {
    func testAccountTransitionClearsHistoryAndDiscardsPendingGeneration() async {
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
        viewModel.messages = [.init(role: "assistant", content: "Prior account response")]
        viewModel.draft = "Private prompt"

        let generation = Task { await viewModel.sendOnDevice() }
        await runtime.waitUntilStarted()
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
    private var startWaiters: [CheckedContinuation<Void, Never>] = []
    private var responseWaiter: CheckedContinuation<String, Never>?

    func status() async throws -> AndroidModelState {
        .available
    }

    func download() async throws -> AndroidModelState {
        .available
    }

    func generate(prompt _: String) async throws -> String {
        started = true
        for waiter in startWaiters {
            waiter.resume()
        }
        startWaiters = []
        return await withCheckedContinuation { responseWaiter = $0 }
    }

    func waitUntilStarted() async {
        guard !started else { return }
        await withCheckedContinuation { startWaiters.append($0) }
    }

    func finish(_ response: String) {
        responseWaiter?.resume(returning: response)
        responseWaiter = nil
    }
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
