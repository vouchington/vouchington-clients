@testable import VouchaAndroid
import VouchaCore
import XCTest

@MainActor
final class EndpointSelectionViewModelTests: XCTestCase {
    func testSelectingUnsavedEndpointPersistsProfileAndSelectionTogether() async {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let viewModel = ViewModel(
            secretStore: EndpointSelectionSecrets(),
            settingsStore: settingsStore,
            endpointClient: EndpointSelectionClient()
        )
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"

        await viewModel.selectProvider(.endpoint)

        XCTAssertEqual(viewModel.selectedProvider, .endpoint)
        let configuration = settingsStore.load()
        XCTAssertEqual(configuration.selectedProviderID, AndroidChatProvider.endpoint.rawValue)
        XCTAssertEqual(configuration.selectedEndpoint?.endpoint, "https://models.example.test")
    }
}

private final class EndpointSelectionSecrets: AndroidEndpointSecretStoring {
    func string(forKey _: String) throws -> String? {
        nil
    }

    func set(_: String, forKey _: String) throws {}
    func removeValue(forKey _: String) throws {}
}

private actor EndpointSelectionClient: AndroidEndpointGenerating {
    func generateAssistantResponse(
        message _: String,
        history _: [LocalLLMChatMessage],
        endpoint _: LocalLLMEndpointProfile,
        apiKey _: String?
    ) async throws -> String? {
        nil
    }
}
