@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsLocalLLMDeleteFailureTests: NativeRouteSurfaceViewModelTestCase {
    func testLocalLLMEndpointDeleteReportsPersistenceFailure() async throws {
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            endpoints: [.init(id: endpointID, endpoint: "http://localhost:2999/v1")],
            selectedEndpointID: endpointID
        )
        let persistedStore = await makeLocalLLMSettingsStore(configuration: configuration)
        let failingStore = LocalLLMSettingsStore(
            fileURL: persistedStore.fileURL,
            secretStore: InMemoryLocalLLMSecretStore(),
            configurationWriter: { _, _ in false }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: failingStore)

        await viewModel.deleteLocalLLMEndpoint(id: endpointID)

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertEqual(failingStore.load().endpoint(id: endpointID)?.endpoint, "http://localhost:2999/v1")
        XCTAssertEqual(failingStore.load().selectedEndpointID, endpointID)
    }

    func testLocalLLMEndpointDeleteReloadsStateAfterStagedSecretCleanupFailure() async throws {
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: endpointID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                )
            ],
            selectedEndpointID: endpointID,
            selectedProviderID: "openai_compatible:\(endpointID.uuidString.lowercased())"
        )
        let persistedStore = await makeLocalLLMSettingsStore(configuration: configuration)
        let failingStore = LocalLLMSettingsStore(
            fileURL: persistedStore.fileURL,
            secretStore: FailingClearLocalLLMSecretStore(),
            configurationWriter: { LocalLLMSettingsStore.write($1, to: $0) }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: failingStore)
        await viewModel.loadLocalLLMSettings()

        await viewModel.deleteLocalLLMEndpoint(id: endpointID)

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertFalse(viewModel.localLLMEndpoints.first(where: { $0.id == endpointID })?.isEnabled ?? true)
        XCTAssertNil(viewModel.localLLMSelectedEndpointID)
    }
}

private struct FailingClearLocalLLMSecretStore: LocalLLMSecretStoring {
    func readAPIKey(for _: UUID) async -> String? {
        nil
    }

    func saveAPIKey(_: String, for _: UUID) async throws {}
    func clearAPIKey(for _: UUID) async throws {
        throw TestSecretStoreError.failed
    }

    func migrateLegacyAPIKey(to _: UUID) async throws {}
    func clearLegacyAPIKey() async throws {}
}

private enum TestSecretStoreError: Error {
    case failed
}
