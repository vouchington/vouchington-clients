import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeDynamicConfigSurfaceTests: XCTestCase {
    override func setUp() {
        super.setUp()
        resetProtocol()
    }

    func testSurfaceRendersFilteredNamespaceSelection() async throws {
        configureNamespaceList()
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()
        await viewModel.load()
        await viewModel.select("recaptcha-config")
        viewModel.query = "captcha"

        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "reCAPTCHA"))
        XCTAssertNoThrow(try sut.inspect().find(text: "recaptcha-config"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Feature Flags"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Read only"))
        XCTAssertNoThrow(try sut.inspect().find(text: "block threshold"))
    }

    func testReadOnlyTypedFieldsRenderDisabledControlsWithoutOverrides() async throws {
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()
        await viewModel.select("recaptcha-config")

        let sut = NativeDynamicConfigSurface(viewModel: viewModel)
        let toggles = try sut.inspect().findAll(ViewType.Toggle.self)
        let save = try sut.inspect().find(button: "Save")

        XCTAssertEqual(toggles.count, 2)
        XCTAssertTrue(toggles[0].isDisabled())
        XCTAssertTrue(toggles[1].isDisabled())
        XCTAssertNoThrow(try sut.inspect().find(ViewType.TextField.self))
        XCTAssertTrue(save.isDisabled())
        XCTAssertThrowsError(try sut.inspect().find(ViewType.Picker.self))
        XCTAssertThrowsError(try sut.inspect().find(button: "Clear local overrides"))
    }

    func testStringAndNumericConstraintsRenderThroughTypedEditors() async throws {
        configureNamespace("app-attestation-config", fixture: "native.dynamic-config.namespace.string")
        configureNamespace("post-content-limits-config", fixture: "native.dynamic-config.namespace.integer")
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()

        await viewModel.select("app-attestation-config")
        var sut = NativeDynamicConfigSurface(viewModel: viewModel)
        XCTAssertNoThrow(try sut.inspect().find(text: "request signing mode"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.TextField.self))

        await viewModel.select("post-content-limits-config")
        let integer = try XCTUnwrap(viewModel.selectedNamespace?.fields.first)
        viewModel.drafts[integer.name] = "5.5"
        await viewModel.saveDraft(field: integer)
        sut = NativeDynamicConfigSurface(viewModel: viewModel)
        XCTAssertNoThrow(try sut.inspect().find(text: "Enter a whole number"))

        await viewModel.select("recaptcha-config")
        let number = try XCTUnwrap(viewModel.selectedNamespace?.fields.last)
        viewModel.drafts[number.name] = "1.5"
        await viewModel.saveDraft(field: number)
        sut = NativeDynamicConfigSurface(viewModel: viewModel)
        XCTAssertNoThrow(try sut.inspect().find(text: "Maximum is 1"))
    }

    func testFeatureFlagHistoryRendersWithoutDeviceOverrideControls() async throws {
        configureFeatureFlags()
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")

        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertThrowsError(try sut.inspect().find(ViewType.Picker.self))
        XCTAssertNoThrow(try sut.inspect().find(text: "qa-developer"))
        XCTAssertNoThrow(try sut.inspect().find(text: "fediverse: false → true"))
    }

    func testMutationLockDisablesRenderedGlobalControls() async throws {
        configureFeatureFlags(updateDelay: 0.2)
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let namespace = try XCTUnwrap(viewModel.selectedNamespace)
        let field = try XCTUnwrap(namespace.fields.first { $0.name == "fediverse" })

        async let save: Void = viewModel.saveBoolean(field: field, value: true)
        try await Task.sleep(nanoseconds: 30_000_000)
        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertTrue(viewModel.isSaving)
        XCTAssertTrue(try sut.inspect().find(ViewType.Toggle.self).isDisabled())
        XCTAssertThrowsError(try sut.inspect().find(ViewType.Picker.self))
        await save
    }

    func testNoOpFeedbackRendersWithoutReplacingHistory() async throws {
        configureFeatureFlags()
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })

        await viewModel.saveBoolean(field: field, value: false)
        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "No change to save"))
        XCTAssertNoThrow(try sut.inspect().find(text: "qa-developer"))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasSuffix("/history") }.count,
            1
        )
    }

    func testRenderedChangedWriteRefreshesHistory() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags/history"] = [
            (Data("{\"history\":[]}".utf8), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.history.default"), 200, 0)
        ]
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        try sut.inspect().findAll(ViewType.Toggle.self).last?.tap()
        try await Task.sleep(for: .milliseconds(50))
        let updated = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertNoThrow(try updated.inspect().find(text: "Dynamic config updated"))
        XCTAssertNoThrow(try updated.inspect().find(text: "qa-developer"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "PATCH" }.count, 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasSuffix("/history") }.count, 2)
    }

    func testRenderedFailedWriteKeepsEditedDraft() async throws {
        let writableNamespace = try writableFixture("native.dynamic-config.namespace.string")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/app-attestation-config"] = [
            (writableNamespace, 200, 0),
            (Data("{}".utf8), 503, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/app-attestation-config/history"] = (
            Data("{\"history\":[]}".utf8), 200
        )
        let viewModel = makeViewModel()
        await viewModel.select("app-attestation-config")
        let sut = NativeDynamicConfigSurface(viewModel: viewModel)

        try sut.inspect().find(ViewType.TextField.self).setInput("preserve this draft")
        try sut.inspect().find(button: "Save").tap()
        try await Task.sleep(for: .milliseconds(50))
        let failed = NativeDynamicConfigSurface(viewModel: viewModel)

        XCTAssertEqual(try failed.inspect().find(ViewType.TextField.self).input(), "preserve this draft")
        XCTAssertNoThrow(try failed.inspect().find(text: "Failed to update dynamic config"))
    }

    private func makeViewModel() -> NativeDynamicConfigViewModel {
        let client = APIClient(
            config: .init(baseURL: URL(string: "https://example.test")!),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        return NativeDynamicConfigViewModel(client: client)
    }

    private func configureNamespaceList() {
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces"] = (
            ApiFixtureLoader.data("native.dynamic-config.namespaces.developer"), 200
        )
    }

    private func configureNamespace(_ namespace: String, fixture: String) {
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/\(namespace)"] = (
            ApiFixtureLoader.data(fixture), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/\(namespace)/history"] = (
            Data("{\"history\":[]}".utf8), 200
        )
    }

    private func configureFeatureFlags(updateDelay: TimeInterval = 0) {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, updateDelay)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/feature-flags/history"] = (
            ApiFixtureLoader.data("native.dynamic-config.history.default"), 200
        )
    }

    private func writableFixture(_ name: String) throws -> Data {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(with: ApiFixtureLoader.data(name)) as? [String: Any]
        )
        var namespace = try XCTUnwrap(object["namespace"] as? [String: Any])
        namespace["can_update"] = true
        object["namespace"] = namespace
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func resetProtocol() {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }
}
