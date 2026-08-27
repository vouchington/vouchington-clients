import Foundation
@testable import VouchaAndroid
@testable import VouchaCore
import XCTest

@MainActor
final class EndpointFirstCredentialTests: XCTestCase {
    func testNewEndpointStaysDisabledUntilCredentialWriteSucceeds() async throws {
        let fileURL = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let settingsStore = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())
        let secrets = FirstCredentialAndroidSecrets()
        let viewModel = ViewModel(secretStore: secrets, settingsStore: settingsStore)
        viewModel.endpoint = "https://models.example.test"
        viewModel.endpointModel = "local-model"
        viewModel.endpointAPIKey = "first-key"

        do {
            try await viewModel.saveEndpointSettings()
            XCTFail("Expected the initial credential write to fail")
        } catch {}

        XCTAssertFalse(try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID)).isEnabled)
        XCTAssertNil(settingsStore.load().selectedEndpointID)
        XCTAssertNil(try secrets.string(forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)))

        try await viewModel.saveEndpointSettings()

        XCTAssertTrue(try XCTUnwrap(settingsStore.load().endpoint(id: viewModel.endpointProfileID)).isEnabled)
        XCTAssertEqual(settingsStore.load().selectedEndpointID, viewModel.endpointProfileID)
        XCTAssertEqual(
            try secrets.string(forKey: viewModel.endpointSecretKey(for: viewModel.endpointProfileID)),
            "first-key"
        )
    }
}

private final class FirstCredentialAndroidSecrets: AndroidEndpointSecretStoring {
    private var values: [String: String] = [:]
    private var shouldFailFirstSet = true

    func string(forKey key: String) throws -> String? {
        values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        if shouldFailFirstSet {
            shouldFailFirstSet = false
            throw FirstCredentialAndroidError.failed
        }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        values[key] = nil
    }
}

private enum FirstCredentialAndroidError: Error {
    case failed
}
