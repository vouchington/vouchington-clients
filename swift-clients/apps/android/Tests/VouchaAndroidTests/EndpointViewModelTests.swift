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
        let context = try await makeEndpointGeneratingViewModel(response: "Endpoint answer")
        defer { AndroidTranscriptURLProtocol.uninstall(host: context.transcriptClient.host) }

        await context.viewModel.sendOnDevice()

        XCTAssertEqual(context.viewModel.messages.last?.content, "Endpoint answer")
        let endpointCallCount = await context.endpointClient.callCount()
        XCTAssertEqual(endpointCallCount, 1)
    }

    func testEndpointFailureRestoresDraftWithoutAICoreFallback() async throws {
        let runtime = FakeAICoreRuntime(state: .available, response: "Unexpected fallback")
        let context = try await makeEndpointGeneratingViewModel(
            runtime: runtime,
            error: TestError.failed
        )
        defer { AndroidTranscriptURLProtocol.uninstall(host: context.transcriptClient.host) }

        await context.viewModel.sendOnDevice()

        XCTAssertEqual(context.viewModel.draft, "Endpoint request")
        XCTAssertTrue(context.viewModel.messages.isEmpty)
        let endpointCallCount = await context.endpointClient.callCount()
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
    ) async throws -> EndpointGeneratingTestContext {
        let transcriptClient = try AndroidTranscriptTestFactory.makeClient()
        AndroidTranscriptTestFactory.enqueueEmptyConversationCreation(on: transcriptClient.recorder)
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: transcriptClient.recorder,
            userText: "Endpoint request",
            assistantText: response
        )
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
        await viewModel.bindAccount(client: transcriptClient.client, userID: "fixture-user")
        viewModel.draft = "Endpoint request"
        return EndpointGeneratingTestContext(
            viewModel: viewModel,
            endpointClient: endpointClient,
            transcriptClient: transcriptClient
        )
    }
}
