import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaLocalization
import XCTest

@MainActor
final class AndroidProviderErrorRecoveryTests: XCTestCase {
    func testEndpointSelectionHidesOnlyAICoreErrorUntilProviderReturns() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        let viewModel = AndroidTranscriptTestFactory.makeViewModel(runtime: UnavailableAICoreRuntime())
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        await viewModel.refreshStatus()
        let retainedError = try XCTUnwrap(viewModel.modelErrorMessage)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        viewModel.draft = "Ready for the endpoint"
        try await viewModel.saveEndpointSettings()

        await viewModel.selectProvider(.endpoint)

        XCTAssertTrue(viewModel.canSendTranscriptMessage)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.chatErrorMessageKey)
        XCTAssertEqual(viewModel.modelErrorMessage, retainedError)
        fixture.recorder.enqueue(
            path: "/api/v1/my/conversations/\(AndroidTranscriptTestFactory.newConversationID)/messages",
            data: AndroidTranscriptTestFactory.emptyMessagePageResponse()
        )
        await viewModel.loadConversationMessages(reset: true)
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNil(viewModel.chatErrorMessageKey)

        await viewModel.selectProvider(.aiCore)
        XCTAssertEqual(viewModel.errorMessage, retainedError)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        viewModel.errorMessage = "Newer settings failure"
        await viewModel.selectProvider(.endpoint)
        XCTAssertEqual(viewModel.errorMessage, "Newer settings failure")
    }

    func testAICoreUnavailableErrorReturnsAfterSuccessfulEndpointSendAndProviderSwitch() async throws {
        let fixture = try AndroidTranscriptTestFactory.makeClient()
        defer { AndroidTranscriptURLProtocol.uninstall(host: fixture.host) }
        let scenario = try await makeEndpointScenario(fixture: fixture)
        let viewModel = scenario.viewModel

        XCTAssertEqual(viewModel.messages.last?.content, "Endpoint answer")
        XCTAssertNil(viewModel.errorMessage)
        XCTAssertNotNil(viewModel.modelErrorMessage)
        let endpointCalls = await scenario.endpoint.callCount()
        XCTAssertEqual(endpointCalls, 1)

        await viewModel.selectProvider(.aiCore)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)
        XCTAssertFalse(viewModel.canSendTranscriptMessage)
        XCTAssertTrue(viewModel.errorMessage?.isEmpty == false)

        viewModel.errorMessage = "A newer settings error"
        await viewModel.selectProvider(.endpoint)
        await viewModel.selectProvider(.aiCore)
        XCTAssertEqual(viewModel.errorMessage, "A newer settings error")
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftEmptyStateUnableToLoad)
    }

    private func makeEndpointScenario(
        fixture: AndroidTranscriptTestClient
    ) async throws -> EndpointModelErrorScenario {
        try AndroidTranscriptTestFactory.enqueueExistingConversation(on: fixture.recorder)
        AndroidTranscriptTestFactory.enqueueGeneratedTurn(
            on: fixture.recorder,
            userText: "Endpoint question",
            assistantText: "Endpoint answer"
        )
        let runtime = UnavailableAICoreRuntime()
        let secrets = FakeEndpointSecrets()
        let settings = LocalLLMSettingsStore(
            fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let endpoint = FakeEndpointClient(response: "Endpoint answer")
        let viewModel = ViewModel(
            runtime: runtime,
            secretStore: secrets,
            settingsStore: settings,
            endpointClient: endpoint
        )
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        await viewModel.refreshStatus()
        XCTAssertEqual(viewModel.modelState, .unavailable)
        XCTAssertEqual(viewModel.chatErrorMessageKey, .nativeSwiftAndroidModelUnavailable)

        try await viewModel.saveEndpointSettings()
        await viewModel.selectProvider(.endpoint)
        await viewModel.bindAccount(client: fixture.client, userID: "fixture-user")
        viewModel.draft = "Endpoint question"
        await viewModel.sendOnDevice()
        return EndpointModelErrorScenario(viewModel: viewModel, endpoint: endpoint)
    }
}

private struct UnavailableAICoreRuntime: AndroidAICoreRunning {
    func status() async throws -> AndroidModelState {
        throw RuntimeUnavailable()
    }

    func download() async throws -> AndroidModelState {
        .unavailable
    }

    func generate(prompt _: String) async throws -> String {
        "Unexpected fallback"
    }
}

private struct RuntimeUnavailable: Error {}

private struct EndpointModelErrorScenario {
    let viewModel: ViewModel
    let endpoint: FakeEndpointClient
}
