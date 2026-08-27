@testable import VouchaAndroid
@testable import VouchaCore
import XCTest

@MainActor
final class EndpointPersistenceFailureTests: XCTestCase {
    func testOriginChangeWriteFailurePreservesTheStoredProfileAndSecret() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let secrets = PersistenceFailureSecrets()
        let originalViewModel = ViewModel(secretStore: secrets, settingsStore: settingsStore)
        originalViewModel.endpoint = "https://models.example.test"
        originalViewModel.endpointModel = "local-model"
        originalViewModel.endpointAPIKey = "profile-key"
        try await originalViewModel.saveEndpointSettings()
        let original = settingsStore.load()
        let failingStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore(),
            configurationWriter: { _, _ in false }
        )
        let viewModel = ViewModel(secretStore: secrets, settingsStore: failingStore)
        viewModel.endpoint = "https://other.example.test"

        var didFail = false
        do {
            try await viewModel.saveEndpointSettings()
        } catch AndroidAICoreError.endpointSaveFailed {
            didFail = true
        }

        XCTAssertTrue(didFail)
        XCTAssertEqual(settingsStore.load(), original)
        XCTAssertEqual(
            try secrets.string(forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)),
            "profile-key"
        )
    }
}

private final class PersistenceFailureSecrets: AndroidEndpointSecretStoring {
    private var values: [String: String] = [:]

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
