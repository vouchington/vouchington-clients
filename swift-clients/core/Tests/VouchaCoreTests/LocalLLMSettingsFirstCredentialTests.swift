import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMSettingsFirstCredentialTests: XCTestCase {
    func testNewEndpointStaysDisabledUntilCredentialWriteSucceeds() async {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let endpointID = UUID()
        let secrets = FirstCredentialFailingSecretStore()
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: secrets)
        let configuration = LocalLLMConfiguration(
            endpoints: [.init(id: endpointID, isEnabled: true, endpoint: "https://models.example.test/v1")],
            selectedEndpointID: endpointID
        )

        let didSave = await store.save(configuration, apiKeys: [endpointID: "first-key"])

        XCTAssertFalse(didSave)
        XCTAssertFalse(store.load().endpoint(id: endpointID)?.isEnabled ?? true)
        XCTAssertNil(store.load().selectedEndpointID)
        let missingAPIKey = await secrets.readAPIKey(for: endpointID)
        XCTAssertNil(missingAPIKey)

        let didRetry = await store.save(configuration, apiKeys: [endpointID: "first-key"])

        XCTAssertTrue(didRetry)
        XCTAssertTrue(store.load().endpoint(id: endpointID)?.isEnabled ?? false)
        XCTAssertEqual(store.load().selectedEndpointID, endpointID)
        let savedAPIKey = await secrets.readAPIKey(for: endpointID)
        XCTAssertEqual(savedAPIKey, "first-key")
    }
}

private actor FirstCredentialFailingSecretStore: LocalLLMSecretStoring {
    private var apiKeys: [UUID: String] = [:]
    private var shouldFailFirstSave = true

    func readAPIKey(for endpointID: UUID) async -> String? {
        apiKeys[endpointID]
    }

    func saveAPIKey(_ apiKey: String, for endpointID: UUID) async throws {
        if shouldFailFirstSave {
            shouldFailFirstSave = false
            throw FirstCredentialError.failed
        }
        apiKeys[endpointID] = apiKey
    }

    func clearAPIKey(for endpointID: UUID) async throws {
        apiKeys[endpointID] = nil
    }
}

private enum FirstCredentialError: Error {
    case failed
}
