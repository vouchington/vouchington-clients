@testable import VouchaAndroid
@testable import VouchaCore
import XCTest

@MainActor
final class EndpointOriginChangeSecretTests: XCTestCase {
    func testSuccessfulOriginChangeWithoutReplacementNeverRestoresTheOldSecret() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let secrets = FailingCleanupSecrets()
        let viewModel = ViewModel(secretStore: secrets, settingsStore: settingsStore)
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        viewModel.endpointAPIKey = "old-origin-key"
        try await viewModel.saveEndpointSettings()

        viewModel.endpoint = "https://other.example.test"
        try await viewModel.saveEndpointSettings()
        try await viewModel.saveEndpointSettings()

        XCTAssertEqual(viewModel.endpointAPIKey, "")
        XCTAssertNil(try secrets.string(
            forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)
        ))
    }

    func testOriginChangeRetryNeverSavesTheOldSecretToTheNewOrigin() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let secrets = FailingCleanupSecrets()
        let viewModel = ViewModel(secretStore: secrets, settingsStore: settingsStore)
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        viewModel.endpointAPIKey = "old-origin-key"
        try await viewModel.saveEndpointSettings()

        viewModel.endpoint = "https://other.example.test"
        secrets.failNextRemoval = true
        var didFail = false
        do {
            try await viewModel.saveEndpointSettings()
        } catch {
            didFail = true
        }
        XCTAssertTrue(didFail)
        XCTAssertEqual(viewModel.endpointAPIKey, "")
        let tombstone = try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID))
        XCTAssertFalse(tombstone.isEnabled)
        XCTAssertNil(settingsStore.load().selectedEndpointID)
        XCTAssertNil(settingsStore.load().selectedProviderID)

        secrets.failNextRemoval = true
        do {
            try await viewModel.saveEndpointSettings()
            XCTFail("Expected the repeated cleanup failure to keep the endpoint disabled")
        } catch {}
        XCTAssertFalse(try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID)).isEnabled)

        try await viewModel.saveEndpointSettings()
        XCTAssertNil(try secrets.string(forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)))
    }

    func testReplacementWriteFailureKeepsTheEndpointDisabledUntilRetry() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let secrets = FailingCleanupSecrets()
        let viewModel = ViewModel(secretStore: secrets, settingsStore: settingsStore)
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        viewModel.endpointAPIKey = "old-origin-key"
        try await viewModel.saveEndpointSettings()

        viewModel.endpoint = "https://other.example.test"
        viewModel.endpointAPIKey = "replacement-key"
        secrets.failNextSet = true
        do {
            try await viewModel.saveEndpointSettings()
            XCTFail("Expected the replacement write to fail")
        } catch {}

        XCTAssertFalse(try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID)).isEnabled)
        XCTAssertNil(settingsStore.load().selectedEndpointID)
        XCTAssertNil(settingsStore.load().selectedProviderID)
        XCTAssertEqual(viewModel.endpointAPIKey, "replacement-key")

        try await viewModel.saveEndpointSettings()
        XCTAssertTrue(try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID)).isEnabled)
        XCTAssertEqual(
            try secrets.string(forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)),
            "replacement-key"
        )
    }
}

private final class FailingCleanupSecrets: AndroidEndpointSecretStoring {
    var failNextRemoval = false
    var failNextSet = false
    private var values: [String: String] = [:]

    func string(forKey key: String) throws -> String? {
        values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        if failNextSet {
            failNextSet = false
            throw FailingCleanupError.failed
        }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        if failNextRemoval {
            failNextRemoval = false
            throw FailingCleanupError.failed
        }
        values[key] = nil
    }
}

private enum FailingCleanupError: Error {
    case failed
}
