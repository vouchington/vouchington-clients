import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
@testable import VouchaPersistence
import XCTest

@MainActor
final class DynamicConfigViewModelTests: XCTestCase {
    func testSearchAndSelectionIgnoreStaleResponses() async throws {
        resetProtocol()
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces"] = (
            ApiFixtureLoader.data("native.dynamic-config.namespaces.developer"), 200
        )
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/recaptcha-config"] = [
            (ApiFixtureLoader.data("native.dynamic-config.namespace.typed"), 200, 0.15)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/recaptcha-config/history"] = [
            (Self.emptyHistory, 200, 0.15)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/app-attestation-config"] = (
            ApiFixtureLoader.data("native.dynamic-config.namespace.string"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/app-attestation-config/history"] = (
            Self.emptyHistory, 200
        )
        let viewModel = makeViewModel()
        await viewModel.load()

        XCTAssertEqual(viewModel.namespaces.map(\.namespace), ["feature-flags", "recaptcha-config"])

        async let stale: Void = viewModel.select("recaptcha-config")
        try await Task.sleep(for: .milliseconds(20))
        await viewModel.select("app-attestation-config")
        await stale

        XCTAssertEqual(viewModel.selectedNamespace?.namespace, "app-attestation-config")
        viewModel.query = "captcha"
        XCTAssertEqual(viewModel.filteredNamespaces.map(\.namespace), ["recaptcha-config"])
    }

    func testSelectionInProgressDisablesOldFieldsAndBlocksSave() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        configureNamespace("app-attestation-config", fixture: "native.dynamic-config.namespace.string")
        suspendNamespaceResponse("app-attestation-config")
        defer { releaseNamespaceResponse("app-attestation-config") }
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let oldNamespace = try XCTUnwrap(viewModel.selectedNamespace)
        let oldField = try XCTUnwrap(oldNamespace.fields.first { $0.name == "fediverse" })

        async let selection: Void = viewModel.select("app-attestation-config")
        await waitForSuspendedNamespaceResponse("app-attestation-config")

        XCTAssertTrue(viewModel.isSelecting)
        XCTAssertTrue(NativeDynamicConfigFieldRow(
            viewModel: viewModel,
            namespace: oldNamespace,
            field: oldField
        ).isEditingDisabled)

        await viewModel.saveBoolean(field: oldField, value: true)

        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
        releaseNamespaceResponse("app-attestation-config")
        await selection
        XCTAssertFalse(viewModel.isSelecting)
        XCTAssertEqual(viewModel.selectedNamespace?.namespace, "app-attestation-config")
    }

    func testStaleSelectionCannotClearNewerSelectionInProgress() async {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        configureNamespace("app-attestation-config", fixture: "native.dynamic-config.namespace.string")
        suspendNamespaceResponse("recaptcha-config")
        suspendNamespaceResponse("app-attestation-config")
        defer {
            releaseNamespaceResponse("recaptcha-config")
            releaseNamespaceResponse("app-attestation-config")
        }
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")

        async let stale: Void = viewModel.select("recaptcha-config")
        await waitForSuspendedNamespaceResponse("recaptcha-config")
        async let current: Void = viewModel.select("app-attestation-config")
        await waitForSuspendedNamespaceResponse("app-attestation-config")

        releaseNamespaceResponse("recaptcha-config")
        await stale

        XCTAssertTrue(viewModel.isSelecting)
        XCTAssertEqual(viewModel.selectedNamespace?.namespace, "feature-flags")
        releaseNamespaceResponse("app-attestation-config")
        await current
        XCTAssertFalse(viewModel.isSelecting)
        XCTAssertEqual(viewModel.selectedNamespace?.namespace, "app-attestation-config")
    }

    func testValidationPreservesInvalidDrafts() async throws {
        resetProtocol()
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()
        await viewModel.select("recaptcha-config")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.last)
        viewModel.drafts[field.name] = "not-a-number"

        await viewModel.saveDraft(field: field)

        XCTAssertEqual(viewModel.drafts[field.name], "not-a-number")
        XCTAssertEqual(viewModel.validationErrors[field.name], "Enter a finite number")
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
    }

    func testNumericDraftRoundTripsWithoutLosingPrecision() async throws {
        resetProtocol()
        let value = 0.123_456_789_012_345_68
        let namespace = try preciseNumericNamespace(value: value)
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/recaptcha-config"] = (namespace, 200)
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/recaptcha-config/history"] = (
            Self.emptyHistory, 200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/recaptcha-config"] = try [
            (namespace, 200, 0),
            (updateResponse(namespace: namespace), 200, 0)
        ]
        let viewModel = makeViewModel()
        await viewModel.select("recaptcha-config")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.last)

        XCTAssertEqual(viewModel.drafts[field.name], String(value))

        await viewModel.saveDraft(field: field)

        XCTAssertEqual(viewModel.drafts[field.name], String(value))
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.last??.contains(String(value)) == true)
    }

    func testSaveSeedsNewResponseFieldsWhilePreservingSiblingDrafts() async throws {
        resetProtocol()
        let initial = try draftMergeNamespace(savedValue: 0.5, siblingValue: "server-old", newValue: nil)
        let updated = try draftMergeNamespace(savedValue: 0.8, siblingValue: "server-new", newValue: "observe")
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/recaptcha-config/history"] = (
            Self.emptyHistory, 200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/recaptcha-config"] = try [
            (initial, 200, 0),
            (updateResponse(namespace: updated), 200, 0)
        ]
        let viewModel = makeViewModel()
        await viewModel.select("recaptcha-config")
        let saved = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "block_threshold" })
        viewModel.drafts[saved.name] = "0.75"
        viewModel.drafts["sibling_mode"] = "local-sibling"
        viewModel.drafts["removed_mode"] = "stale-removed"
        viewModel.drafts["enabled"] = "stale-boolean"

        await viewModel.saveDraft(field: saved)

        XCTAssertEqual(viewModel.drafts[saved.name], "0.8")
        XCTAssertEqual(viewModel.drafts["sibling_mode"], "local-sibling")
        XCTAssertEqual(viewModel.drafts["new_mode"], "observe")
        XCTAssertNil(viewModel.drafts["removed_mode"])
        XCTAssertNil(viewModel.drafts["enabled"])
    }

    func testSaveDistinguishesChangedAndNoOpResponses() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0)
        ]
        let featureFlags = makeFeatureFlagState()
        let viewModel = makeViewModel(featureFlags: featureFlags)
        await viewModel.select("feature-flags")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })

        await viewModel.saveBoolean(field: field, value: true)
        XCTAssertEqual(viewModel.feedbackMessage, "Dynamic config updated")
        XCTAssertEqual(featureFlags.effective["fediverse"], true)

        let refreshedField = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })
        await viewModel.saveBoolean(field: refreshedField, value: false)
        XCTAssertEqual(viewModel.feedbackMessage, "No change to save")
    }

    func testSaveUsesOneGlobalMutationLock() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0.1)
        ]
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })

        async let first: Void = viewModel.saveBoolean(field: field, value: true)
        await Task.yield()
        await viewModel.saveBoolean(field: field, value: false)
        await first

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "PATCH" }.count, 1)
    }

    func testMutationCompletionDoesNotReplaceNewerNamespaceSelection() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        configureNamespace("app-attestation-config", fixture: "native.dynamic-config.namespace.string")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0.15)
        ]
        let featureFlags = makeFeatureFlagState()
        let viewModel = makeViewModel(featureFlags: featureFlags)
        await viewModel.select("feature-flags")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })

        async let mutation: Void = viewModel.saveBoolean(field: field, value: true)
        try await Task.sleep(for: .milliseconds(20))
        await viewModel.select("app-attestation-config")
        await mutation

        XCTAssertEqual(viewModel.selectedNamespace?.namespace, "app-attestation-config")
        XCTAssertNil(viewModel.feedbackMessage)
        XCTAssertEqual(featureFlags.effective["fediverse"], true)
    }

    func testDelayedPublicFetchCannotOverwriteNewerDynamicConfigWrite() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/feature-flags"] = [
            (ApiFixtureLoader.data("native.feature-flags.default"), 200, 0.15),
            (ApiFixtureLoader.data("native.feature-flags.default"), 200, 0)
        ]
        let featureFlags = makeFeatureFlagState()
        let dynamicConfig = makeViewModel(featureFlags: featureFlags)
        let overrides = NativeFeatureFlagOverridesViewModel(
            client: makeClient(),
            featureFlags: featureFlags,
            canOverride: true
        )
        await dynamicConfig.select("feature-flags")
        let field = try XCTUnwrap(
            dynamicConfig.selectedNamespace?.fields.first { $0.name == "fediverse" }
        )

        async let staleFetch: Void = overrides.load()
        while !CannedFeedURLProtocol.hasCapturedRequest(path: "/api/v1/feature-flags") {
            await Task.yield()
        }
        await dynamicConfig.saveBoolean(field: field, value: true)
        await staleFetch

        XCTAssertEqual(featureFlags.effective["fediverse"], true)

        await overrides.load()
        XCTAssertEqual(featureFlags.effective["fediverse"], false)
    }

    func testSuccessfulSaveReportsHistoryRefreshFailureWithoutRetryingWrite() async throws {
        resetProtocol()
        configureNamespace("feature-flags", fixture: "native.dynamic-config.update.no-op")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags"] = [
            (ApiFixtureLoader.data("native.dynamic-config.update.no-op"), 200, 0),
            (ApiFixtureLoader.data("native.dynamic-config.update.changed"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/dynamic-config/namespaces/feature-flags/history"] = [
            (Self.emptyHistory, 200, 0),
            (Data("{}".utf8), 503, 0)
        ]
        let viewModel = makeViewModel()
        await viewModel.select("feature-flags")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first { $0.name == "fediverse" })

        await viewModel.saveBoolean(field: field, value: true)

        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.filter { $0 == "PATCH" }.count, 1)
        XCTAssertEqual(viewModel.feedbackMessage, "Dynamic config updated")
        XCTAssertEqual(viewModel.errorMessage, "Dynamic config updated, but history could not be refreshed")
        XCTAssertEqual(viewModel.selectedNamespace?.config["fediverse"], .boolean(true))
    }

    func testOverridesMergeOnlyKnownRemoteFlagsAndPersist() throws {
        let fileURL = temporaryFileURL()
        let store = FeatureFlagOverrideFileStore(fileURL: fileURL)
        try store.set(true, for: "removedFlag")
        try store.set(true, for: "fediverse")
        let state = FeatureFlagState(store: store)
        XCTAssertEqual(state.effective, [:])

        state.applyRemote(["fediverse": false, "chat": false])

        XCTAssertEqual(state.effective, ["fediverse": true, "chat": false])
        XCTAssertNil(state.localOverride(for: "removedFlag"))
        try state.setLocalOverride(false, for: "chat")
        XCTAssertEqual(FeatureFlagOverrideFileStore(fileURL: fileURL).load()["chat"], false)

        let recreatedState = FeatureFlagState(store: FeatureFlagOverrideFileStore(fileURL: fileURL))
        recreatedState.applyRemote(["fediverse": false, "chat": true])
        XCTAssertEqual(recreatedState.localOverride(for: "chat"), false)
        XCTAssertEqual(recreatedState.effective["chat"], false)
    }

    func testReadOnlyNamespaceDoesNotExposeMutationsOrOverrides() async throws {
        resetProtocol()
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()
        await viewModel.select("recaptcha-config")
        let field = try XCTUnwrap(viewModel.selectedNamespace?.fields.first)

        await viewModel.saveBoolean(field: field, value: false)

        XCTAssertEqual(viewModel.selectedNamespace?.canUpdate, false)
        XCTAssertFalse(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
    }

    func testReadOnlyStringAndNumericEditorsAreDisabled() async throws {
        resetProtocol()
        configureNamespace("app-attestation-config", fixture: "native.dynamic-config.namespace.string")
        configureNamespace("recaptcha-config", fixture: "native.dynamic-config.namespace.typed")
        let viewModel = makeViewModel()
        await viewModel.select("app-attestation-config")
        let stringNamespace = try XCTUnwrap(viewModel.selectedNamespace)
        let stringField = try XCTUnwrap(stringNamespace.fields.first { $0.type == .string })

        XCTAssertTrue(NativeDynamicConfigFieldRow(
            viewModel: viewModel,
            namespace: stringNamespace,
            field: stringField
        ).isEditingDisabled)

        await viewModel.select("recaptcha-config")
        let numericNamespace = try XCTUnwrap(viewModel.selectedNamespace)
        let numericField = try XCTUnwrap(numericNamespace.fields.first { $0.type == .number })
        XCTAssertTrue(NativeDynamicConfigFieldRow(
            viewModel: viewModel,
            namespace: numericNamespace,
            field: numericField
        ).isEditingDisabled)
    }

    private func makeViewModel(featureFlags: FeatureFlagState? = nil) -> NativeDynamicConfigViewModel {
        NativeDynamicConfigViewModel(
            client: makeClient(),
            onNamespaceUpdated: { featureFlags?.applyGlobalNamespace($0) }
        )
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: .init(baseURL: URL(string: "https://example.test")!),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
    }

    private func makeFeatureFlagState() -> FeatureFlagState {
        FeatureFlagState(store: FeatureFlagOverrideFileStore(fileURL: temporaryFileURL()))
    }

    private func configureNamespace(_ namespace: String, fixture: String) {
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/\(namespace)"] = (
            ApiFixtureLoader.data(fixture), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/dynamic-config/namespaces/\(namespace)/history"] = (
            Self.emptyHistory, 200
        )
    }

    private func suspendNamespaceResponse(_ namespace: String) {
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/dynamic-config/namespaces/\(namespace)")
    }

    private func releaseNamespaceResponse(_ namespace: String) {
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/dynamic-config/namespaces/\(namespace)")
    }

    private func waitForSuspendedNamespaceResponse(_ namespace: String) async {
        while !CannedFeedURLProtocol.hasSuspendedResponse(
            path: "/api/v1/dynamic-config/namespaces/\(namespace)"
        ) {
            await Task.yield()
        }
    }

    private func preciseNumericNamespace(value: Double) throws -> Data {
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.dynamic-config.namespace.typed")
            ) as? [String: Any]
        )
        var namespace = try XCTUnwrap(response["namespace"] as? [String: Any])
        var config = try XCTUnwrap(namespace["config"] as? [String: Any])
        var fields = try XCTUnwrap(namespace["fields"] as? [[String: Any]])
        config["block_threshold"] = value
        fields[2]["value"] = value
        namespace["can_update"] = true
        namespace["config"] = config
        namespace["fields"] = fields
        response["namespace"] = namespace
        return try JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])
    }

    private func updateResponse(namespace: Data) throws -> Data {
        var response = try XCTUnwrap(JSONSerialization.jsonObject(with: namespace) as? [String: Any])
        response["changed"] = false
        return try JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])
    }

    private func draftMergeNamespace(savedValue: Double, siblingValue: String, newValue: String?) throws -> Data {
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: preciseNumericNamespace(value: savedValue)) as? [String: Any]
        )
        var namespace = try XCTUnwrap(response["namespace"] as? [String: Any])
        var fields = try XCTUnwrap(namespace["fields"] as? [[String: Any]])
        fields.append(["name": "sibling_mode", "type": "string", "description": "Sibling", "value": siblingValue])
        if let newValue {
            fields.append(["name": "new_mode", "type": "string", "description": "New", "value": newValue])
        }
        namespace["fields"] = fields
        response["namespace"] = namespace
        return try JSONSerialization.data(withJSONObject: response, options: [.sortedKeys])
    }

    private func resetProtocol() {
        CannedFeedURLProtocol.discardPendingResponses()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func temporaryFileURL() -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
            .appendingPathComponent("feature-flags.json")
    }

    private static let emptyHistory = Data("{\"history\":[]}".utf8)
}
