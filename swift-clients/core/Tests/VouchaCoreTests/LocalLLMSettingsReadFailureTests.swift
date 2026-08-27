import Foundation
@testable import VouchaCore
import XCTest

final class LocalLLMSettingsReadFailureTests: XCTestCase {
    func testUpdateDoesNotOverwriteMalformedConfiguration() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let malformedData = Data("{\"isEnabled\":".utf8)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try malformedData.write(to: fileURL)
        let store = LocalLLMSettingsStore(fileURL: fileURL, secretStore: InMemoryLocalLLMSecretStore())

        let didUpdate = await store.update { configuration in
            configuration.selectedProviderID = "apple-foundation-models"
        }

        XCTAssertFalse(didUpdate)
        XCTAssertEqual(try Data(contentsOf: fileURL), malformedData)
        XCTAssertEqual(store.load(), .disabled)
    }

    func testUpdateDoesNotOverwriteUnreadableConfiguration() async throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        let fileURL = directory.appendingPathComponent("local-llm-settings.json")
        let originalData = Data("{\"isEnabled\":true}".utf8)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try originalData.write(to: fileURL)
        let store = LocalLLMSettingsStore(
            fileURL: fileURL,
            secretStore: InMemoryLocalLLMSecretStore(),
            configurationWriter: { LocalLLMSettingsStore.write($1, to: $0) },
            configurationReader: { _ in .failed }
        )

        let didUpdate = await store.update { configuration in
            configuration.selectedProviderID = "apple-foundation-models"
        }

        XCTAssertFalse(didUpdate)
        XCTAssertEqual(try Data(contentsOf: fileURL), originalData)
    }
}
