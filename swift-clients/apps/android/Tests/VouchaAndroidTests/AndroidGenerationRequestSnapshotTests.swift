import Foundation
import Observation
@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class AndroidGenerationRequestSnapshotTests: XCTestCase {
    func testAdmittedGenerationFreezesProviderInputsWhileOldCancellationCompletes() async throws {
        for provider in [AndroidChatProvider.aiCore, .endpoint] {
            let started = expectation(description: "Old generation started")
            let cancelled = expectation(description: "Old generation cancelled")
            let runtime = CancellableGenerationRuntime(started: started, cancelled: cancelled, prematureStart: nil)
            defer { runtime.releaseOldGeneration() }
            let model = try await makeModel(provider: provider, runtime: runtime)
            let initial = try AndroidTranscriptTestFactory.makeClient()
            let replacement = try AndroidTranscriptTestFactory.makeClient()
            defer {
                AndroidTranscriptURLProtocol.uninstall(host: initial.host)
                AndroidTranscriptURLProtocol.uninstall(host: replacement.host)
            }
            AndroidTranscriptTestFactory.enqueueEmptyConversationCreation(on: initial.recorder)
            AndroidTranscriptTestFactory.enqueueEmptyConversationCreation(on: replacement.recorder)
            AndroidTranscriptTestFactory.enqueueGeneratedTurn(
                on: replacement.recorder, userText: "Admitted prompt", assistantText: "New account answer"
            )
            await model.bindAccount(client: initial.client, userID: "old-user")
            model.modelState = .available
            model.draft = "Old account prompt"
            let old = Task { await model.sendOnDevice() }
            await fulfillment(of: [started], timeout: 2)
            await model.bindAccount(client: replacement.client, userID: "new-user")
            let created = await model.createConversation()
            XCTAssertTrue(created)
            await fulfillment(of: [cancelled], timeout: 2)
            model.messages = [.init(role: "assistant", content: "Original conversation")]
            model.draft = "Admitted prompt"
            let admitted = expectation(description: "New send reached cancellation-completion wait")
            withObservationTracking { _ = model.activeGenerationID } onChange: { admitted.fulfill() }
            let new = Task { await model.sendOnDevice() }
            await fulfillment(of: [admitted], timeout: 2)
            model.messages = [.init(role: "assistant", content: "Different conversation")]
            model.endpoint = "https://replacement.example.test"
            model.endpointModel = "replacement-model"
            model.endpointAPIKey = "replacement-key"
            try await model.saveEndpointSettings()
            if provider == .aiCore { model.selectedProvider = .endpoint }
            runtime.releaseOldGeneration()
            await old.value
            await new.value
            try assertOriginalInput(runtime.requestInput, provider: provider)
            try assertPersistedInput(initial: initial.recorder, replacement: replacement.recorder, provider: provider)
        }
    }

    private func assertPersistedInput(
        initial: AndroidTranscriptRecorder, replacement: AndroidTranscriptRecorder, provider: AndroidChatProvider
    ) throws {
        let path = "/api/v1/conversations/\(AndroidTranscriptTestFactory.newConversationID)/client-generated-chat"
        XCTAssertTrue(initial.requestSnapshot(path: path).isEmpty)
        let request = try XCTUnwrap(replacement.requestSnapshot(path: path).first)
        let body = try XCTUnwrap(request.httpBody).jsonObject()
        XCTAssertEqual(body["model_provider"] as? String, provider == .aiCore ? "android_aicore" : "openai_compatible")
        if provider == .endpoint { XCTAssertEqual(body["model_name"] as? String, "original-model") }
    }

    private func makeModel(
        provider: AndroidChatProvider,
        runtime: CancellableGenerationRuntime
    ) async throws -> ViewModel {
        let model = ViewModel(
            runtime: runtime, secretStore: MemorySecrets(),
            settingsStore: LocalLLMSettingsStore(
                fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
                secretStore: InMemoryLocalLLMSecretStore()
            ), endpointClient: runtime
        )
        model.endpoint = "https://original.example.test"
        model.endpointModel = "original-model"
        model.endpointAPIKey = "original-key"
        try await model.saveEndpointSettings()
        await model.selectProvider(provider)
        model.modelState = .available
        return model
    }

    private func assertOriginalInput(_ input: CancellableGenerationInput?, provider: AndroidChatProvider) throws {
        switch try XCTUnwrap(input) {
        case let .aiCore(prompt):
            XCTAssertEqual(provider, .aiCore)
            XCTAssertTrue(prompt.contains("Original conversation"))
            XCTAssertTrue(prompt.contains("Admitted prompt"))
            XCTAssertFalse(prompt.contains("Different conversation"))
        case let .endpoint(message, history, endpoint, apiKey):
            XCTAssertEqual(provider, .endpoint)
            XCTAssertEqual(message, "Admitted prompt")
            XCTAssertEqual(history.map(\.content), ["Original conversation"])
            XCTAssertEqual(endpoint.endpoint, "https://original.example.test")
            XCTAssertEqual(endpoint.selectedModel, "original-model")
            XCTAssertEqual(apiKey, "original-key")
        }
    }
}
