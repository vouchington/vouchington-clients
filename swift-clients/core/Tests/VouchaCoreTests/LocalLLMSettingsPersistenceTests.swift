import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMSettingsPersistenceTests: XCTestCase {
    func testSettingsStorePreservesSecretsAndStoredConfigurationWhenWriteFails() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = InMemoryLocalLLMSecretStore()
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://first.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let changed = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://second.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let didSaveOriginal = await store.save(original, apiKeys: [endpointID: "secret"])
        XCTAssertTrue(didSaveOriginal)

        let failingStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secrets,
            configurationWriter: { _, _ in false }
        )

        let didSaveChanged = await failingStore.save(changed)
        XCTAssertFalse(didSaveChanged)
        XCTAssertEqual(store.load(), original)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "secret")
        let persisted = try JSONDecoder().decode(LocalLLMConfiguration.self, from: Data(contentsOf: fileURL))
        XCTAssertEqual(persisted, original)
    }

    func testOriginChangeDoesNotPersistWhenOldSecretCannotBeCleared() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FailingLocalLLMSecretStore(apiKeys: [endpointID: "secret"], failClears: true)
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://first.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let changed = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://second.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let didSaveOriginal = await initialStore.save(original)
        XCTAssertTrue(didSaveOriginal)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        let didSaveChanged = await store.save(changed)
        XCTAssertFalse(didSaveChanged)
        let safeConfiguration = store.load()
        XCTAssertFalse(safeConfiguration.endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(safeConfiguration.selectedEndpointID)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "secret")
    }

    func testProfileRemovalDoesNotClearSecretWhenConfigurationWriteFails() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = InMemoryLocalLLMSecretStore()
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://models.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let didSaveOriginal = await initialStore.save(original, apiKeys: [endpointID: "secret"])
        XCTAssertTrue(didSaveOriginal)
        let failingStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secrets,
            configurationWriter: { _, _ in false }
        )

        let didRemove = await failingStore.save(.disabled)
        XCTAssertFalse(didRemove)
        XCTAssertEqual(initialStore.load(), original)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "secret")
    }

    func testProfileRemovalRetainsDisabledTombstoneWhenSecretCleanupFails() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FailingLocalLLMSecretStore(apiKeys: [endpointID: "secret"], failClears: true)
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                isEnabled: true,
                endpoint: "https://models.example.test/v1"
            )],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let didSaveOriginal = await initialStore.save(original)
        XCTAssertTrue(didSaveOriginal)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        let didRemove = await store.save(.disabled)

        XCTAssertFalse(didRemove)
        XCTAssertFalse(store.load().endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(store.load().selectedEndpointID)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "secret")
    }

    func testOriginChangeStaysDisabledWhenReplacementSecretCannotBeSaved() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FailingLocalLLMSecretStore(apiKeys: [endpointID: "old-secret"], failSaves: true)
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://first.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let changed = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                isEnabled: true,
                endpoint: "https://second.example.test/v1"
            )],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let didSaveOriginal = await initialStore.save(original)
        XCTAssertTrue(didSaveOriginal)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        let didSaveChanged = await store.save(changed, apiKeys: [endpointID: "replacement-secret"])

        XCTAssertFalse(didSaveChanged)
        XCTAssertFalse(store.load().endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(store.load().selectedEndpointID)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertNil(retainedSecret)
    }

    func testClearRetainsDisabledTombstoneWhenSecretCleanupFails() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FailingLocalLLMSecretStore(apiKeys: [endpointID: "secret"], failClears: true)
        let configuration = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                isEnabled: true,
                endpoint: "https://models.example.test/v1"
            )],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let didSaveConfiguration = await initialStore.save(configuration)
        XCTAssertTrue(didSaveConfiguration)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        let didClear = await store.clear()

        XCTAssertFalse(didClear)
        XCTAssertFalse(store.load().endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(store.load().selectedEndpointID)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "secret")
    }

    func testClearLeavesDisabledConfigurationWhenFinalRemovalFails() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = InMemoryLocalLLMSecretStore()
        let configuration = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://models.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let initialStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let didSaveConfiguration = await initialStore.save(configuration, apiKeys: [endpointID: "secret"])
        XCTAssertTrue(didSaveConfiguration)
        let failingStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: secrets,
            configurationWriter: { LocalLLMSettingsStore.write($1, to: $0) },
            configurationRemover: { _ in false }
        )

        let didClear = await failingStore.clear()
        XCTAssertFalse(didClear)
        XCTAssertFalse(initialStore.load().endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(initialStore.load().selectedEndpointID)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertNil(retainedSecret)
    }

    func testReenablingWithABlankKeyKeepsTheEndpointDisabledWhenSecretClearFails() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FailingLocalLLMSecretStore(apiKeys: [endpointID: "stale-key"], failClears: true)
        let disabled = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [
                LocalLLMEndpointProfile(
                    id: endpointID,
                    isEnabled: false,
                    endpoint: "http://localhost:2999/v1",
                    modelNames: ["gpt-oss"],
                    selectedModelName: "gpt-oss"
                )
            ]
        )
        let seed = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let didSeed = await seed.save(disabled)
        XCTAssertTrue(didSeed)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        var enabled = disabled
        enabled.endpoints[0].isEnabled = true

        let didSave = await store.save(enabled, apiKeys: [endpointID: ""])

        XCTAssertFalse(didSave)
        XCTAssertFalse(store.load().endpoint(id: endpointID)?.isEnabled ?? true)
        let retainedSecret = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(retainedSecret, "stale-key")
    }

}

private actor FailingLocalLLMSecretStore: LocalLLMSecretStoring {
    private var apiKeys: [UUID: String]
    private let failClears: Bool
    private let failSaves: Bool

    init(apiKeys: [UUID: String], failClears: Bool = false, failSaves: Bool = false) {
        self.apiKeys = apiKeys
        self.failClears = failClears
        self.failSaves = failSaves
    }

    func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async throws {
        if failSaves {
            throw TestSecretStoreError.failed
        }
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async throws {
        if failClears {
            throw TestSecretStoreError.failed
        }
        apiKeys[endpointID] = nil
    }
}

private enum TestSecretStoreError: Error {
    case failed
}
