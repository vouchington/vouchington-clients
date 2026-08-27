import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsLocalLLMOriginChangeTests: NativeRouteSurfaceViewModelTestCase {
    func testOriginChangeDoesNotReuseProgrammaticallyLoadedAPIKey() async throws {
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID),
            apiKey: "old-key"
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let loadedAPIKey = await viewModel.loadLocalLLMEndpointAPIKey(id: endpointID)

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: true,
                endpoint: "http://localhost:3000/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: loadedAPIKey
            ),
            apiKeyWasEdited: false
        )

        XCTAssertEqual(store.load().endpoint(id: endpointID)?.endpoint, "http://localhost:3000/v1")
        let clearedAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedAPIKey)

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: true,
                endpoint: "http://localhost:3000/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: loadedAPIKey
            ),
            apiKeyWasEdited: false
        )
        let retriedAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(retriedAPIKey)
    }

    func testOriginChangePersistsExplicitAPIKeyReplacement() async throws {
        let endpointID = UUID()
        let store = await makeLocalLLMSettingsStore(
            configuration: makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID),
            apiKey: "old-key"
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: true,
                endpoint: "http://localhost:3000/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: "replacement-key"
            ),
            apiKeyWasEdited: true
        )

        XCTAssertEqual(store.load().endpoint(id: endpointID)?.endpoint, "http://localhost:3000/v1")
        let replacementAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertEqual(replacementAPIKey, "replacement-key")
    }

    func testOriginChangeRetryDoesNotRestoreLoadedKeyAfterFinalWriteFailure() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = InMemoryLocalLLMSecretStore()
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let didSaveInitialConfiguration = await initialStore.save(
            makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID),
            apiKeys: [endpointID: "old-key"]
        )
        XCTAssertTrue(didSaveInitialConfiguration)
        let writer = FinalConfigurationWriteFailer()
        let store = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secrets,
            configurationWriter: { writer.write($0, $1) }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let loadedAPIKey = await viewModel.loadLocalLLMEndpointAPIKey(id: endpointID)

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: true,
                endpoint: "http://localhost:3000/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: loadedAPIKey
            ),
            apiKeyWasEdited: false
        )
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")

        await viewModel.updateLocalLLMEndpoint(
            id: endpointID,
            draft: LocalLLMEndpointDraft(
                displayName: "",
                isEnabled: true,
                endpoint: "http://localhost:3000/v1",
                modelsText: "gpt-oss",
                selectedModel: "gpt-oss",
                apiKey: loadedAPIKey
            ),
            apiKeyWasEdited: false
        )

        XCTAssertEqual(store.load().endpoint(id: endpointID)?.endpoint, "http://localhost:3000/v1")
        let storedKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(storedKey)
    }
}

private final class FinalConfigurationWriteFailer: @unchecked Sendable {
    private let lock = NSLock()
    private var writeCount = 0

    func write(_ fileURL: URL, _ configuration: LocalLLMConfiguration) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        writeCount += 1
        guard writeCount != 2 else { return false }
        return LocalLLMSettingsStore.write(configuration, to: fileURL)
    }
}
