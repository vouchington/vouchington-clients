@testable import VouchaAndroid
@testable import VouchaCore
import XCTest

@MainActor
final class EndpointViewModelTests: XCTestCase {
    func testEndpointProfileReloadsAfterSaving() async throws {
        let context = makeEndpointSettingsViewModel()
        context.viewModel.endpointAPIKey = "profile-key"

        try await context.viewModel.saveEndpointSettings()

        let reloaded = ViewModel(
            secretStore: context.secrets,
            settingsStore: context.settingsStore,
            endpointClient: FakeEndpointClient(response: "")
        )
        XCTAssertEqual(reloaded.endpoint, "https://models.example.test")
        XCTAssertEqual(reloaded.endpointModel, "local-model")
        XCTAssertEqual(reloaded.endpointAPIKey, "profile-key")
        XCTAssertEqual(
            context.settingsStore.load().selectedEndpoint?.id,
            context.settingsStore.load().endpoints.first?.id
        )
    }

    func testEmptyEndpointKeyClearsTheScopedSecret() async throws {
        let context = makeEndpointSettingsViewModel()
        context.viewModel.endpointAPIKey = "profile-key"
        try await context.viewModel.saveEndpointSettings()

        context.viewModel.endpointAPIKey = ""
        try await context.viewModel.saveEndpointSettings()

        XCTAssertTrue(context.secrets.values.isEmpty)
    }

    func testOriginChangeSavesAnExplicitReplacementScopedSecret() async throws {
        let context = makeEndpointSettingsViewModel()
        context.viewModel.endpointAPIKey = "profile-key"
        try await context.viewModel.saveEndpointSettings()

        context.viewModel.endpoint = "https://other.example.test"
        context.viewModel.endpointAPIKey = "replacement-key"
        try await context.viewModel.saveEndpointSettings()

        XCTAssertEqual(
            try context.secrets.string(
                forKey: context.viewModel.endpointSecretKey(for: context.viewModel.endpointProfileID)
            ),
            "replacement-key"
        )
        XCTAssertEqual(context.viewModel.endpointAPIKey, "replacement-key")
    }

    func testProviderSelectionPersistsAndRestoresOnlyWhenExplicit() async throws {
        let context = makeEndpointSettingsViewModel()
        try await context.viewModel.saveEndpointSettings()
        XCTAssertEqual(
            ViewModel(
                secretStore: context.secrets,
                settingsStore: context.settingsStore,
                endpointClient: FakeEndpointClient(response: "")
            ).selectedProvider,
            .aiCore
        )

        await context.viewModel.selectProvider(.endpoint)

        XCTAssertEqual(
            ViewModel(
                secretStore: context.secrets,
                settingsStore: context.settingsStore,
                endpointClient: FakeEndpointClient(response: "")
            ).selectedProvider,
            .endpoint
        )
    }

    func testEndpointGenerationUsesConfiguredEndpoint() async throws {
        let (viewModel, endpointClient) = try await makeEndpointGeneratingViewModel(response: "Endpoint answer")

        await viewModel.sendOnDevice()

        XCTAssertEqual(viewModel.messages.last?.content, "Endpoint answer")
        let endpointCallCount = await endpointClient.callCount()
        XCTAssertEqual(endpointCallCount, 1)
    }

    func testEndpointFailureRestoresDraftWithoutAICoreFallback() async throws {
        let runtime = FakeAICoreRuntime(state: .available, response: "Unexpected fallback")
        let (viewModel, endpointClient) = try await makeEndpointGeneratingViewModel(
            runtime: runtime,
            error: TestError.failed
        )

        await viewModel.sendOnDevice()

        XCTAssertEqual(viewModel.draft, "Endpoint request")
        XCTAssertTrue(viewModel.messages.isEmpty)
        let endpointCallCount = await endpointClient.callCount()
        let runtimeCallCount = await runtime.generationSnapshot().callCount
        XCTAssertEqual(endpointCallCount, 1)
        XCTAssertEqual(runtimeCallCount, 0)
    }

    private func makeEndpointSettingsViewModel() -> EndpointSettingsTestContext {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let secrets = FakeEndpointSecrets()
        let viewModel = ViewModel(
            secretStore: secrets,
            settingsStore: settingsStore,
            endpointClient: FakeEndpointClient(response: "")
        )
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        return .init(viewModel: viewModel, settingsStore: settingsStore, secrets: secrets)
    }

    private func makeEndpointGeneratingViewModel(
        runtime: FakeAICoreRuntime = FakeAICoreRuntime(state: .available),
        response: String = "",
        error: Error? = nil
    ) async throws -> (ViewModel, FakeEndpointClient) {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let secrets = FakeEndpointSecrets()
        let endpointClient = FakeEndpointClient(response: response, error: error)
        let viewModel = ViewModel(
            runtime: runtime,
            secretStore: secrets,
            settingsStore: settingsStore,
            endpointClient: endpointClient
        )
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        try await viewModel.saveEndpointSettings()
        await viewModel.selectProvider(.endpoint)
        viewModel.draft = "Endpoint request"
        return (viewModel, endpointClient)
    }
}

private struct EndpointSettingsTestContext {
    let viewModel: ViewModel
    let settingsStore: LocalLLMSettingsStore
    let secrets: FakeEndpointSecrets
}

private final class FakeEndpointSecrets: AndroidEndpointSecretStoring {
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

private actor FakeEndpointClient: AndroidEndpointGenerating {
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
