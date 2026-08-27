import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMSettingsStoreTests: XCTestCase {
    func testSettingsStorePersistsSecretsAndClearsLocalFiles() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let secretStore = InMemoryLocalLLMSecretStore()
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secretStore)
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            isEnabled: true,
            endpoints: [LocalLLMEndpointProfile(
                id: endpointID,
                endpoint: "http://localhost:11434/v1",
                modelNames: ["gpt-oss"],
                selectedModelName: "gpt-oss"
            )],
            selectedEndpointID: endpointID
        )

        let didSave = await store.save(configuration, apiKeys: [endpointID: " local-key "])
        XCTAssertTrue(didSave)

        XCTAssertEqual(store.load(), configuration)
        let storedAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertEqual(storedAPIKey, "local-key")

        let didClear = await store.save(configuration, apiKeys: [endpointID: " "])
        XCTAssertTrue(didClear)
        let clearedAPIKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedAPIKey)

        let didClearStore = await store.clear()
        XCTAssertTrue(didClearStore)
        XCTAssertEqual(store.load(), .disabled)
        let clearedAfterStoreClear = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedAfterStoreClear)
        XCTAssertFalse(FileManager.default.fileExists(atPath: fileURL.path))
    }

    func testSettingsStoreClearsSecretWhenEndpointOriginChanges() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secretStore = InMemoryLocalLLMSecretStore()
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secretStore)
        let original = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://first.example.test/v1")],
            selectedEndpointID: endpointID
        )
        let changed = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://second.example.test/v1")],
            selectedEndpointID: endpointID
        )

        let didSaveOriginal = await store.save(original, apiKeys: [endpointID: "secret"])
        let didSaveChanged = await store.save(changed)
        XCTAssertTrue(didSaveOriginal)
        XCTAssertTrue(didSaveChanged)

        let clearedAfterOriginChange = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedAfterOriginChange)
    }

    func testSettingsStoreClearsSecretWhenEndpointProfileIsDeleted() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let store = LocalLLMSettingsStore(
            fileURL: directory.appendingPathComponent("local-llm-settings.json"),
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let endpointID = UUID()
        let configuration = LocalLLMConfiguration(
            endpoints: [LocalLLMEndpointProfile(id: endpointID, endpoint: "https://models.example.test/v1")],
            selectedEndpointID: endpointID
        )

        let didSave = await store.save(configuration, apiKeys: [endpointID: "secret"])
        let didDisable = await store.save(.disabled)
        XCTAssertTrue(didSave)
        XCTAssertTrue(didDisable)

        let deletedProfileKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(deletedProfileKey)
        XCTAssertEqual(store.load(), .disabled)
    }

    func testSettingsStoreKeepsTheLatestDuplicateEndpointIDWithoutTrapping() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let store = LocalLLMSettingsStore(
            fileURL: directory.appendingPathComponent("local-llm-settings.json"),
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let endpointID = UUID()
        let first = LocalLLMEndpointProfile(id: endpointID, endpoint: "https://first.example.test/v1")
        let second = LocalLLMEndpointProfile(id: endpointID, endpoint: "https://second.example.test/v1")
        let configuration = LocalLLMConfiguration(
            endpoints: [first, second],
            selectedEndpointID: endpointID
        )

        let didSave = await store.save(configuration)
        XCTAssertTrue(didSave)

        XCTAssertEqual(store.load().endpoints, [second])
        XCTAssertEqual(store.load().selectedEndpointID, endpointID)
    }

    func testSettingsStoreSerializesConcurrentReadModifyWriteTransactions() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let store = LocalLLMSettingsStore(
            fileURL: directory.appendingPathComponent("local-llm-settings.json"),
            secretStore: InMemoryLocalLLMSecretStore()
        )

        async let enable: Bool = store.update { configuration in
            configuration.isEnabled = true
        }
        async let select: Bool = store.update { configuration in
            configuration.selectedProviderID = "apple-foundation-models"
        }
        let (enabled, selected) = await (enable, select)
        XCTAssertTrue(enabled)
        XCTAssertTrue(selected)

        let configuration = store.load()
        XCTAssertTrue(configuration.isEnabled)
        XCTAssertEqual(configuration.selectedProviderID, "apple-foundation-models")
    }

    func testSettingsStoreSerializesTransactionsAcrossInstancesForTheSameFile() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let firstSecrets = BlockingSecretStore()
        let firstStore = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: firstSecrets
        )
        let secondStore = LocalLLMSettingsStore(
            fileURL: fileURL.standardizedFileURL,
            secretStore: InMemoryLocalLLMSecretStore()
        )
        let didSave = await firstStore.save(
            .init(endpoints: [.init(id: endpointID, endpoint: "https://first.example.test")])
        )
        XCTAssertTrue(didSave)

        let firstUpdate = Task.detached {
            await firstStore.update { configuration in
                configuration.endpoints = []
                configuration.isEnabled = true
            }
        }
        await firstSecrets.waitUntilClearStarts()

        let secondMutationEntered = DispatchSemaphore(value: 0)
        let secondUpdate = Task.detached {
            await secondStore.update { configuration in
                secondMutationEntered.signal()
                configuration.selectedProviderID = "apple-foundation-models"
            }
        }

        XCTAssertEqual(
            secondMutationEntered.wait(timeout: .now() + 0.2),
            .timedOut,
            "A second store must not load or mutate this file while the first transaction is blocked."
        )

        await firstSecrets.releaseClear()
        _ = await firstUpdate.value
        XCTAssertEqual(secondMutationEntered.wait(timeout: .now() + 2), .success)
        _ = await secondUpdate.value

        let configuration = firstStore.load()
        XCTAssertTrue(configuration.isEnabled)
        XCTAssertTrue(configuration.endpoints.isEmpty)
        XCTAssertEqual(configuration.selectedProviderID, "apple-foundation-models")
    }

    func testSettingsStoreDirectSecretAccessTrimsAndClearsBlankValues() async {
        let store = LocalLLMSettingsStore(secretStore: InMemoryLocalLLMSecretStore())
        let endpointID = UUID()

        let didSaveDirectKey = await store.saveAPIKey(" direct-key ", for: endpointID)
        XCTAssertTrue(didSaveDirectKey)
        let trimmedKey = await store.readAPIKey(for: endpointID)
        XCTAssertEqual(trimmedKey, "direct-key")

        let didClearBlankKey = await store.saveAPIKey(" \n", for: endpointID)
        XCTAssertTrue(didClearBlankKey)
        let blankKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(blankKey)

        let didSaveAnotherKey = await store.saveAPIKey("another-key", for: endpointID)
        XCTAssertTrue(didSaveAnotherKey)
        let didClearDirectKey = await store.clearAPIKey(for: endpointID)
        XCTAssertTrue(didClearDirectKey)
        let clearedKey = await store.readAPIKey(for: endpointID)
        XCTAssertNil(clearedKey)
    }

    func testSettingsStoreReturnsDisabledForMalformedSettingsFile() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data("not-json".utf8).write(to: fileURL)

        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())

        XCTAssertEqual(store.load(), .disabled)
    }

    func testSettingsStoreMigratesLegacyConfigurationAndSecretWithoutDataLoss() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try legacyConfigurationData().write(to: fileURL)
        let secrets = InMemoryLocalLLMSecretStore(legacyAPIKey: "legacy-key")
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        let configuration = store.load()
        let endpointID = try XCTUnwrap(configuration.selectedEndpointID)

        XCTAssertEqual(endpointID, LocalLLMSettingsStore.legacyEndpointID)
        XCTAssertEqual(
            configuration.selectedProviderID,
            "openai_compatible:\(endpointID.uuidString.lowercased())"
        )
        XCTAssertEqual(configuration.endpoints, [
            .init(
                id: endpointID,
                isEnabled: true,
                endpoint: "http://localhost:11434/v1",
                modelNames: ["gpt-oss", "llama"],
                selectedModelName: "llama"
            )
        ])
        let migratedAPIKey = await store.readAPIKey(for: endpointID)
        let remainingLegacyAPIKey = await secrets.readLegacyAPIKey()
        XCTAssertEqual(migratedAPIKey, "legacy-key")
        XCTAssertNil(remainingLegacyAPIKey)

        let reloaded = store.load()
        XCTAssertEqual(reloaded, configuration)
        let persisted = try JSONDecoder().decode(LocalLLMConfiguration.self, from: Data(contentsOf: fileURL))
        XCTAssertEqual(persisted, configuration)
    }

    func testSettingsStoreMigrationNeverOverwritesExistingScopedSecret() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try legacyConfigurationData().write(to: fileURL)
        let endpointID = LocalLLMSettingsStore.legacyEndpointID
        let secrets = InMemoryLocalLLMSecretStore(
            apiKeys: [endpointID: "scoped-key"],
            legacyAPIKey: "legacy-key"
        )
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)

        XCTAssertEqual(store.load().selectedEndpointID, endpointID)
        let scopedAPIKey = await store.readAPIKey(for: endpointID)
        let remainingLegacyAPIKey = await secrets.readLegacyAPIKey()
        XCTAssertEqual(scopedAPIKey, "scoped-key")
        XCTAssertNil(remainingLegacyAPIKey)
    }

    func testSettingsStoreClearWaitsForLegacyMigrationAndRemovesBothSecrets() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try legacyConfigurationData().write(to: fileURL)
        let secrets = DelayedLegacySecretStore(legacyAPIKey: "legacy-key")
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let endpointID = LocalLLMSettingsStore.legacyEndpointID

        XCTAssertEqual(store.load().selectedEndpointID, endpointID)
        await secrets.waitUntilMigrationStarts()

        let clear = Task {
            _ = await store.clear()
        }
        await secrets.releaseMigration()
        await clear.value

        XCTAssertEqual(store.load(), .disabled)
        let scopedAPIKey = await secrets.readAPIKey(for: endpointID)
        let legacyAPIKey = await secrets.readLegacyAPIKey()
        XCTAssertNil(scopedAPIKey)
        XCTAssertNil(legacyAPIKey)
    }

    func testSettingsStoreBlankLegacySecretSaveWaitsForMigrationAndRemovesBothSecrets() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try legacyConfigurationData().write(to: fileURL)
        let secrets = DelayedLegacySecretStore(legacyAPIKey: "legacy-key")
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let endpointID = LocalLLMSettingsStore.legacyEndpointID

        XCTAssertEqual(store.load().selectedEndpointID, endpointID)
        await secrets.waitUntilMigrationStarts()

        let clear = Task {
            _ = await store.saveAPIKey(" ", for: endpointID)
        }
        await secrets.releaseMigration()
        await clear.value

        let scopedAPIKey = await secrets.readAPIKey(for: endpointID)
        let legacyAPIKey = await secrets.readLegacyAPIKey()
        XCTAssertNil(scopedAPIKey)
        XCTAssertNil(legacyAPIKey)
    }

    #if canImport(Security)
        func testKeychainSecretStoreRoundTripsAPIKey() async throws {
            let store = KeychainLocalLLMSecretStore()
            let endpointID = UUID()

            do {
                try await store.clearAPIKey(for: endpointID)
                try await store.saveAPIKey("local-key", for: endpointID)
            } catch {
                throw XCTSkip("Keychain is unavailable in this test environment.")
            }

            let storedAPIKey = await store.readAPIKey(for: endpointID)
            try XCTSkipIf(storedAPIKey == nil, "Keychain is unavailable in this test environment.")
            XCTAssertEqual(storedAPIKey, "local-key")

            try await store.saveAPIKey("updated-key", for: endpointID)
            let updatedAPIKey = await store.readAPIKey(for: endpointID)
            XCTAssertEqual(updatedAPIKey, "updated-key")

            try await store.clearAPIKey(for: endpointID)
            let clearedAPIKey = await store.readAPIKey(for: endpointID)
            XCTAssertNil(clearedAPIKey)
        }
    #endif

    func testDefaultFileURLUsesApplicationSupportNamespace() {
        let url = LocalLLMSettingsStore.defaultFileURL()

        XCTAssertEqual(url.lastPathComponent, "local-llm-settings.json")
        XCTAssertEqual(url.deletingLastPathComponent().lastPathComponent, "ai.voucha")
    }
}

private func legacyConfigurationData() -> Data {
    Data(
        """
        {"isEnabled":true,"endpoint":"http://localhost:11434/v1","modelNames":["gpt-oss","llama"],"selectedModelName":"llama"}
        """.utf8
    )
}

private actor BlockingSecretStore: LocalLLMSecretStoring {
    private var clearStarted = false
    private var clearStartWaiters: [CheckedContinuation<Void, Never>] = []
    private var clearRelease: CheckedContinuation<Void, Never>?

    func readAPIKey(for _: UUID) async -> String? {
        nil
    }

    func saveAPIKey(_: String, for _: UUID) async {}

    func clearAPIKey(for _: UUID) async {
        clearStarted = true
        let waiters = clearStartWaiters
        clearStartWaiters = []
        waiters.forEach { $0.resume() }
        await withCheckedContinuation { clearRelease = $0 }
    }

    func waitUntilClearStarts() async {
        guard !clearStarted else { return }
        await withCheckedContinuation { clearStartWaiters.append($0) }
    }

    func releaseClear() {
        clearRelease?.resume()
        clearRelease = nil
    }
}
