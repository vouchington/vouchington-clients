import Foundation
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsLocalLLMEndpointMutationTests: NativeRouteSurfaceViewModelTestCase {
    func testSelectLocalLLMEndpointSetsSelectedProviderID() async throws {
        let activeID = UUID()
        let inactiveID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: activeID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                ),
                LocalLLMEndpointProfile(
                    id: inactiveID,
                    isEnabled: true,
                    endpoint: "http://localhost:3000/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                )
            ],
            selectedEndpointID: activeID,
            selectedProviderID: "openai_compatible:\(activeID.uuidString.lowercased())"
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)

        await viewModel.selectLocalLLMEndpoint(id: inactiveID)

        let saved = store.load()
        XCTAssertEqual(saved.selectedEndpointID, inactiveID)
        XCTAssertEqual(saved.selectedProviderID, "openai_compatible:\(inactiveID.uuidString.lowercased())")
        XCTAssertEqual(viewModel.localLLMSelectedEndpointID, inactiveID)
    }

    func testSelectLocalLLMEndpointRejectsActivationWhenGloballyDisabled() async throws {
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: false,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: endpointID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                )
            ]
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let beforeConfiguration = store.load()

        await viewModel.selectLocalLLMEndpoint(id: endpointID)

        XCTAssertEqual(store.load(), beforeConfiguration)
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
    }

    func testSelectLocalLLMEndpointRejectsActivationWhenURLIsInvalid() async throws {
        let validID = UUID()
        let invalidID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: validID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                ),
                LocalLLMEndpointProfile(
                    id: invalidID,
                    isEnabled: true,
                    endpoint: "",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                )
            ],
            selectedEndpointID: validID,
            selectedProviderID: "openai_compatible:\(validID.uuidString.lowercased())"
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let beforeConfiguration = store.load()

        await viewModel.selectLocalLLMEndpoint(id: invalidID)

        XCTAssertEqual(store.load(), beforeConfiguration)
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertFalse(configuration.endpoint(id: invalidID)?.isSelectableAsCurrent ?? true)
    }

    func testSelectLocalLLMEndpointRejectsActivationWhenModelIsMissing() async throws {
        let validID = UUID()
        let invalidID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: validID,
                    isEnabled: true,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                ),
                LocalLLMEndpointProfile(
                    id: invalidID,
                    isEnabled: true,
                    endpoint: "http://localhost:3000/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: ""
                )
            ],
            selectedEndpointID: validID,
            selectedProviderID: "openai_compatible:\(validID.uuidString.lowercased())"
        )
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let beforeConfiguration = store.load()

        await viewModel.selectLocalLLMEndpoint(id: invalidID)

        XCTAssertEqual(store.load(), beforeConfiguration)
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertFalse(configuration.endpoint(id: invalidID)?.isSelectableAsCurrent ?? true)
    }

    func testUpdateLocalLLMEndpointClearsSelectionWhenActiveEndpointBecomesInvalid() async throws {
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
        let store = await makeLocalLLMSettingsStore(configuration: configuration)
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: ""
        )

        let didSave = await viewModel.updateLocalLLMEndpoint(id: endpointID, draft: draft, apiKeyWasEdited: false)

        XCTAssertTrue(didSave)
        let saved = store.load()
        XCTAssertNil(saved.selectedProviderID)
        XCTAssertNil(saved.selectedEndpointID)
        XCTAssertFalse(saved.endpoint(id: endpointID)?.isSelectableAsCurrent ?? true)
    }

    func testSetLocalLLMEnabledRevertsOptimisticStateOnSaveFailure() async throws {
        let store = LocalLLMSettingsStore(
            fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
            secretStore: InMemoryLocalLLMSecretStore(),
            configurationWriter: { _, _ in false }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        XCTAssertFalse(viewModel.localLLMEnabled)

        await viewModel.setLocalLLMEnabled(true)

        XCTAssertFalse(viewModel.localLLMEnabled)
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
    }

    func testSetLocalLLMEnabledRevertsOptimisticStateOnSaveFailureWhenDisabling() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let secretStore = InMemoryLocalLLMSecretStore()
        let seedStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secretStore)
        let didSeed = await seedStore.save(makeLocalLLMConfiguration(isEnabled: true))
        XCTAssertTrue(didSeed)
        let store = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secretStore,
            configurationWriter: { _, _ in false }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        await viewModel.loadLocalLLMSettings()
        XCTAssertTrue(viewModel.localLLMEnabled)

        await viewModel.setLocalLLMEnabled(false)

        XCTAssertTrue(
            viewModel.localLLMEnabled,
            "a failed save must restore the previous enabled state, not hardcode false"
        )
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
    }

    func testTestLocalLLMEndpointInvokesClientWithDraftConfigurationWithoutPersisting() async throws {
        LocalLLMTestURLProtocol.reset(responseData: Data(#"{"output_text":"ready"}"#.utf8))
        let store = await makeLocalLLMSettingsStore()
        let viewModel = try SettingsViewModel(
            client: makeClient(),
            localLLMSettingsStore: store,
            localLLMFeaturePolicy: LocalLLMFeaturePolicy(environment: ["VOUCHA_NATIVE_LOCAL_LLM_ENABLED": "true"]),
            localLLMResponsesClient: OpenAICompatibleResponsesClient(protocolClasses: [LocalLLMTestURLProtocol.self])
        )
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "http://localhost:2999/v1",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: "draft-key"
        )
        let beforeConfiguration = store.load()

        await viewModel.testLocalLLMEndpoint(draft: draft)

        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Local model test passed.")
        XCTAssertEqual(
            LocalLLMTestURLProtocol.capturedRequest?.value(forHTTPHeaderField: "Authorization"),
            "Bearer draft-key"
        )
        XCTAssertEqual(store.load(), beforeConfiguration)
        XCTAssertTrue(viewModel.localLLMEndpoints.isEmpty)
    }

    func testUpdateLocalLLMEndpointPersistsApiKeyWhenEdited() async throws {
        let endpointID = UUID()
        let configuration = makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID)
        let store = await makeLocalLLMSettingsStore(configuration: configuration, apiKey: "stored-key")
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "http://localhost:2999/v1",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: "edited-key"
        )

        let didSave = await viewModel.updateLocalLLMEndpoint(id: endpointID, draft: draft, apiKeyWasEdited: true)

        XCTAssertTrue(didSave)
        let stored = await store.readAPIKey(for: endpointID)
        XCTAssertEqual(stored, "edited-key")
    }

    func testUpdateLocalLLMEndpointDoesNotPersistApiKeyWhenNotEdited() async throws {
        let endpointID = UUID()
        let configuration = makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID)
        let store = await makeLocalLLMSettingsStore(configuration: configuration, apiKey: "stored-key")
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "http://localhost:2999/v1",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: "should-not-be-written"
        )

        let didSave = await viewModel.updateLocalLLMEndpoint(id: endpointID, draft: draft, apiKeyWasEdited: false)

        XCTAssertTrue(didSave)
        let stored = await store.readAPIKey(for: endpointID)
        XCTAssertEqual(
            stored,
            "stored-key",
            "apiKeyWasEdited: false must ignore the draft's apiKey entirely, even when it is non-empty"
        )
    }

    func testUpdateLocalLLMEndpointReturnsFalseAndPreservesKeyOnSaveFailure() async throws {
        let endpointID = UUID()
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let secretStore = InMemoryLocalLLMSecretStore()
        let seedStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secretStore)
        let didSeed = await seedStore.save(
            makeLocalLLMConfiguration(endpoint: "http://localhost:2999/v1", endpointID: endpointID),
            apiKeys: [endpointID: "stored-key"]
        )
        XCTAssertTrue(didSeed)
        let store = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secretStore,
            configurationWriter: { _, _ in false }
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "http://localhost:2999/v1",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: "retry-key"
        )

        let didSave = await viewModel.updateLocalLLMEndpoint(id: endpointID, draft: draft, apiKeyWasEdited: true)

        XCTAssertFalse(didSave, "a failed write must be reported so the caller can keep apiKeyWasEdited set for retry")
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        let stored = await secretStore.readAPIKey(for: endpointID)
        XCTAssertEqual(stored, "stored-key", "a failed save must not persist the edited key")
    }

    func testUpdateLocalLLMEndpointReloadsStateAfterOriginChangeSecretCleanupFailure() async throws {
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
        let draft = LocalLLMEndpointDraft(
            displayName: "Draft Model",
            isEnabled: true,
            endpoint: "http://localhost:3000/v1",
            modelsText: "gpt-oss",
            selectedModel: "gpt-oss",
            apiKey: "new-key"
        )

        let didSave = await viewModel.updateLocalLLMEndpoint(id: endpointID, draft: draft, apiKeyWasEdited: true)

        XCTAssertFalse(didSave)
        XCTAssertEqual(uiEnglish(viewModel.localLLMStatusMessage), "Unable to save local model settings.")
        XCTAssertFalse(
            viewModel.localLLMEndpoints.first(where: { $0.id == endpointID })?.isEnabled ?? true,
            "an origin-change failure must reload the on-disk staged/disabled state, not leave stale in-memory data"
        )
    }

    func testCreateLocalLLMEndpointClearsStaleSecretWhenApiKeyFieldIsBlank() async throws {
        let draftID = UUID()
        let seedConfiguration = LocalLLMConfiguration(isEnabled: true, endpoints: [], selectedEndpointID: draftID)
        let store = await makeLocalLLMSettingsStore(configuration: seedConfiguration, apiKey: "leaked-key")
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        viewModel.localLLMDraftEndpointID = draftID
        viewModel.localLLMDisplayName = "Draft Model"
        viewModel.localLLMEndpoint = "http://localhost:3000/v1"
        viewModel.localLLMModelsText = "gpt-oss"
        viewModel.localLLMSelectedModel = "gpt-oss"
        viewModel.localLLMAPIKey = ""

        await viewModel.createLocalLLMEndpoint()

        let stored = await store.readAPIKey(for: draftID)
        XCTAssertNil(stored, "a blank API key field on create must clear any stale secret for the draft ID")
    }

    func testCreateLocalLLMEndpointPreservesDraftEditedWhileSaveIsInFlight() async throws {
        let secretStore = GatedLocalLLMSecretStore()
        let store = LocalLLMSettingsStore(
            fileURL: FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString),
            secretStore: secretStore
        )
        let viewModel = try SettingsViewModel(client: makeClient(), localLLMSettingsStore: store)
        viewModel.localLLMDisplayName = "Original Name"
        viewModel.localLLMEndpoint = "http://localhost:2999/v1"
        viewModel.localLLMModelsText = "gpt-oss"
        viewModel.localLLMSelectedModel = "gpt-oss"
        viewModel.localLLMAPIKey = "draft-key"
        let draftIDBeforeSave = viewModel.localLLMDraftEndpointID

        let saveTask = Task { await viewModel.createLocalLLMEndpoint() }
        try await waitForLocalLLMEndpointSaveGate { await secretStore.saveStarted }
        viewModel.localLLMDisplayName = "Edited While Saving"
        await secretStore.resumeSave()
        await saveTask.value

        XCTAssertEqual(
            viewModel.localLLMDisplayName,
            "Edited While Saving",
            "an edit made while the save is in flight must not be clobbered by resetLocalLLMDraft()"
        )
        XCTAssertNotEqual(
            viewModel.localLLMDraftEndpointID,
            draftIDBeforeSave,
            "the draft ID must still rotate so a follow-up save creates a new endpoint, not overwrite the just-created one"
        )
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

private actor GatedLocalLLMSecretStore: LocalLLMSecretStoring {
    private(set) var saveStarted = false
    private var continuation: CheckedContinuation<Void, Never>?
    private var apiKeys: [UUID: String] = [:]

    func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async throws {
        saveStarted = true
        await withCheckedContinuation { continuation = $0 }
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async throws {
        apiKeys[endpointID] = nil
    }

    func migrateLegacyAPIKey(to _: UUID) async throws {}
    func clearLegacyAPIKey() async throws {}

    func resumeSave() {
        continuation?.resume()
        continuation = nil
    }
}

private func waitForLocalLLMEndpointSaveGate(
    _ condition: @escaping @Sendable () async -> Bool,
    timeout: Duration = .seconds(2)
) async throws {
    let clock = ContinuousClock()
    let deadline = clock.now.advanced(by: timeout)
    while true {
        if await condition() {
            return
        }
        guard clock.now < deadline else {
            throw LocalLLMEndpointSaveGateTimeout(timeout: timeout)
        }
        try await clock.sleep(for: .milliseconds(10))
    }
}

private struct LocalLLMEndpointSaveGateTimeout: LocalizedError {
    let timeout: Duration

    var errorDescription: String? {
        "Timed out after \(timeout) waiting for the local-LLM endpoint save gate to engage"
    }
}
