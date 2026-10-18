@testable import VouchaAndroid
import VouchaCore

struct EndpointGeneratingTestContext {
    let viewModel: ViewModel
    let endpointClient: FakeEndpointClient
    let transcriptClient: AndroidTranscriptTestClient
}

struct EndpointSettingsTestContext {
    let viewModel: ViewModel
    let settingsStore: LocalLLMSettingsStore
    let secrets: FakeEndpointSecrets
}

final class FakeEndpointSecrets: AndroidEndpointSecretStoring {
    var values: [String: String] = [:]

    func string(forKey key: String) throws -> String? {
        values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        values[key] = nil
    }
}

actor FakeEndpointClient: AndroidEndpointGenerating {
    let response: String
    let error: Error?
    private var calls = 0

    init(response: String, error: Error? = nil) {
        self.response = response
        self.error = error
    }

    func generateAssistantResponse(
        message _: String,
        history _: [LocalLLMChatMessage],
        endpoint _: LocalLLMEndpointProfile,
        apiKey _: String?
    ) async throws -> String? {
        calls += 1
        if let error {
            throw error
        }
        return response
    }

    func callCount() -> Int {
        calls
    }
}
