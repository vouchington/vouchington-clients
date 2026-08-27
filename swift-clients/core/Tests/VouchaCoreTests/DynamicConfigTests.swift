import Foundation
@testable import VouchaAPI
@testable import VouchaPersistence
import XCTest

final class DynamicConfigTests: XCTestCase {
    func testDecodesEveryTypedDynamicConfigFixture() throws {
        let decoder = JSONDecoder.vouchaFixtureDecoder
        let typed = try decoder.decode(
            DynamicConfigNamespaceResponse.self,
            from: ApiFixtureLoader.data("native.dynamic-config.namespace.typed")
        )
        let string = try decoder.decode(
            DynamicConfigNamespaceResponse.self,
            from: ApiFixtureLoader.data("native.dynamic-config.namespace.string")
        )
        let integer = try decoder.decode(
            DynamicConfigNamespaceResponse.self,
            from: ApiFixtureLoader.data("native.dynamic-config.namespace.integer")
        )
        let history = try decoder.decode(
            DynamicConfigHistoryResponse.self,
            from: ApiFixtureLoader.data("native.dynamic-config.history.default")
        )

        XCTAssertEqual(typed.namespace.fields.map(\.type), [.boolean, .boolean, .number])
        XCTAssertEqual(string.namespace.fields.last?.value, .string("off"))
        XCTAssertEqual(integer.namespace.fields.first?.integer, true)
        XCTAssertEqual(history.history.first?.changedBy?.username, "qa-developer")
    }

    func testRejectsUnsupportedDynamicConfigScalarValues() throws {
        XCTAssertThrowsError(try JSONDecoder().decode(DynamicConfigValue.self, from: Data("null".utf8)))
        XCTAssertThrowsError(try JSONDecoder().decode(DynamicConfigValue.self, from: Data("[]".utf8)))
        XCTAssertThrowsError(try JSONDecoder().decode(DynamicConfigValue.self, from: Data("{}".utf8)))
    }

    func testNumericEditableValueRoundTripsWithoutLosingPrecision() throws {
        let value = 0.123_456_789_012_345_68
        let editable = DynamicConfigValue.number(value).editableValue

        XCTAssertEqual(editable, String(value))
        XCTAssertEqual(try XCTUnwrap(Double(editable)), value)
    }

    func testBuildsDynamicConfigEndpointsAndPatchBody() throws {
        XCTAssertEqual(Endpoint.dynamicConfigNamespaces.path, "/api/v1/dynamic-config/namespaces")
        XCTAssertEqual(
            Endpoint.dynamicConfigNamespace("feature/flags").path,
            "/api/v1/dynamic-config/namespaces/feature%2Fflags"
        )
        let update = Endpoint.updateDynamicConfigNamespace("feature-flags", field: "fediverse", value: .boolean(true))
        XCTAssertEqual(update.method, .PATCH)
        XCTAssertEqual(
            try requestBodyJSON(from: update),
            .object(["config": .object(["fediverse": .bool(true)])])
        )
        XCTAssertEqual(
            Endpoint.dynamicConfigHistory("feature-flags").path,
            "/api/v1/dynamic-config/namespaces/feature-flags/history"
        )
    }

    func testPersistentOverridesRoundTripExplicitFalseAndClear() throws {
        let fileURL = temporaryFileURL()
        let store = FeatureFlagOverrideFileStore(fileURL: fileURL)
        try store.set(false, for: "fediverse")
        try store.set(true, for: "chat")
        XCTAssertEqual(store.load(), ["fediverse": false, "chat": true])
        try store.remove("chat")
        XCTAssertEqual(store.load(), ["fediverse": false])
        try store.clear()
        XCTAssertEqual(store.load(), [:])
    }

    func testPersistentOverridesRecoverFromCorruptData() throws {
        let fileURL = temporaryFileURL()
        try FileManager.default.createDirectory(
            at: fileURL.deletingLastPathComponent(),
            withIntermediateDirectories: true
        )
        try Data("not-json".utf8).write(to: fileURL)
        let store = FeatureFlagOverrideFileStore(fileURL: fileURL)
        XCTAssertEqual(store.load(), [:])
        try store.set(true, for: "fediverse")
        XCTAssertEqual(store.load(), ["fediverse": true])
    }

    private func temporaryFileURL() -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
            .appendingPathComponent("feature-flags.json")
    }
}
