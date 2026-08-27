import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
@testable import VouchaPersistence
import XCTest

@MainActor
final class NativeFeatureFlagOverridesSurfaceTests: XCTestCase {
    override func setUp() {
        super.setUp()
        resetProtocol()
    }

    func testRenderedEnabledDisabledInheritedAndClearAllAreDeviceOnly() async throws {
        configureFeatureFlags()
        let state = makeState()
        let viewModel = makeViewModel(state: state)
        await viewModel.load()
        let sut = NativeFeatureFlagOverridesSurface(viewModel: viewModel)
        let pickers = try sut.inspect().findAll(ViewType.Picker.self)
        let chat = pickers[0]
        let fediverse = pickers[2]

        XCTAssertNoThrow(try fediverse.labelView().find(text: "Device override for fediverse"))
        try fediverse.select(value: NativeFeatureFlagOverrideChoice.enabled)
        XCTAssertEqual(state.effective["fediverse"], true)
        try fediverse.select(value: NativeFeatureFlagOverrideChoice.disabled)
        XCTAssertEqual(state.effective["fediverse"], false)
        try chat.select(value: NativeFeatureFlagOverrideChoice.disabled)
        try fediverse.select(value: NativeFeatureFlagOverrideChoice.inherited)
        XCTAssertNil(state.localOverride(for: "fediverse"))
        XCTAssertEqual(state.localOverride(for: "chat"), false)
        try sut.inspect().find(button: "Clear all device overrides").tap()
        XCTAssertTrue(state.localOverrides.isEmpty)

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/feature-flags"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["GET"])
    }

    func testMountedOverridePickerImmediatelyUpdatesFeatureGatedDirectory() async throws {
        configureFeatureFlags()
        let state = makeState()
        let viewModel = makeViewModel(state: state)
        await viewModel.load()
        let sut = FeatureFlagOverrideConsumerHarness(viewModel: viewModel, state: state)

        try await ViewHosting.host(sut) {
            XCTAssertThrowsError(try sut.inspect().find(text: "Fediverse search"))

            try sut.inspect().findAll(ViewType.Picker.self)[2]
                .select(value: NativeFeatureFlagOverrideChoice.enabled)
            await Task.yield()
            XCTAssertNoThrow(try sut.inspect().find(text: "Fediverse search"))

            try sut.inspect().findAll(ViewType.Picker.self)[2]
                .select(value: NativeFeatureFlagOverrideChoice.disabled)
            await Task.yield()
            XCTAssertThrowsError(try sut.inspect().find(text: "Fediverse search"))
        }
    }

    func testUnauthorizedSurfaceHasNoControlsOrNetworkRequest() async throws {
        configureFeatureFlags()
        let viewModel = makeViewModel(state: makeState(), canOverride: false)
        await viewModel.load()
        let sut = NativeFeatureFlagOverridesSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Access required"))
        XCTAssertThrowsError(try sut.inspect().find(ViewType.Picker.self))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testOnlyAdministratorAndDeveloperRolesCanManageDeviceOverrides() {
        XCTAssertTrue(FeatureFlagState.canManageDeviceOverrides(userRoles: ["administrator"]))
        XCTAssertTrue(FeatureFlagState.canManageDeviceOverrides(userRoles: ["developer"]))
        for role in ["moderator", "customer_support", "investor"] {
            XCTAssertFalse(FeatureFlagState.canManageDeviceOverrides(userRoles: [role]))
        }
    }

    func testRenderedLoadAndPersistenceErrorsRemainActionable() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feature-flags"] = (Data("{}".utf8), 503)
        let loadViewModel = makeViewModel(state: makeState())
        await loadViewModel.load()
        let failedLoad = NativeFeatureFlagOverridesSurface(viewModel: loadViewModel)
        XCTAssertNoThrow(try failedLoad.inspect().find(text: "Failed to load feature flags"))
        XCTAssertNoThrow(try failedLoad.inspect().find(button: "Retry feature flags"))

        let state = FeatureFlagState(store: FailingFeatureFlagOverrideStore())
        state.applyRemote(["fediverse": false])
        let saveViewModel = makeViewModel(state: state)
        let failedSave = NativeFeatureFlagOverridesSurface(viewModel: saveViewModel)
        try failedSave.inspect().find(ViewType.Picker.self)
            .select(value: NativeFeatureFlagOverrideChoice.enabled)

        XCTAssertNoThrow(
            try NativeFeatureFlagOverridesSurface(viewModel: saveViewModel)
                .inspect().find(text: "Failed to save device override")
        )
        XCTAssertEqual(state.effective["fediverse"], false)
    }

    func testPreHydrationOverrideReportsExplicitFailure() {
        let state = makeState()

        XCTAssertThrowsError(try state.setLocalOverride(true, for: "fediverse")) {
            XCTAssertEqual($0 as? FeatureFlagOverrideError, .remoteFlagsNotLoaded)
        }
    }

    func testDynamicConfigRouteAndDeviceOverridesAreIndependentNativeSurfaces() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/dynamic-config"))
        let dynamicConfig = NativeRouteDestinationView(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            canOverrideFeatureFlags: true,
            featureFlags: makeState(),
            canCastPublicVotes: true
        )

        XCTAssertNoThrow(try dynamicConfig.inspect().find(NativeDynamicConfigSurface.self))
        XCTAssertThrowsError(try dynamicConfig.inspect().find(NativeFeatureFlagOverridesSurface.self))

        let engineering = NativeFeatureFlagAwareDirectoryView(
            section: .engineering,
            client: makeClient(),
            isSignedIn: true,
            userRoles: ["developer"],
            canCastPublicVotes: true,
            featureFlags: makeState()
        )
        XCTAssertNoThrow(try engineering.inspect().find(text: "Device Feature Flags"))
        XCTAssertFalse(NativeRouteCatalog.entries.contains { $0.auditFamily == "Device Feature Flags" })
    }

    func testRootHookInvalidatesActiveFediverseRouteWhenFlagTurnsOff() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/fediverse"))
        var entry: NativeRouteCatalogEntry? = route.entry
        var match: NativeRouteMatch? = route.match
        var query: String? = "providers=mastodon"

        NativeFeatureFlagRouteInvalidation.clearIfNeeded(
            entry: &entry,
            match: &match,
            query: &query,
            featureFlags: ["fediverse": false]
        )

        XCTAssertNil(entry)
        XCTAssertNil(match)
        XCTAssertNil(query)

        let webRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/web-search"))
        entry = webRoute.entry
        match = webRoute.match
        query = "q=test"
        NativeFeatureFlagRouteInvalidation.clearIfNeeded(
            entry: &entry,
            match: &match,
            query: &query,
            featureFlags: [:]
        )
        XCTAssertEqual(entry?.destinationIdentifier, .webSearch)
        XCTAssertNotNil(match)
        XCTAssertEqual(query, "q=test")
    }

    func testInstanceDetailRequiresFediverseFlagWithoutGatingOrdinaryTopics() throws {
        let instance = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/instance/social-example"))
        XCTAssertTrue(instance.match.requiresFediverseFeature)
        var entry: NativeRouteCatalogEntry? = instance.entry
        var match: NativeRouteMatch? = instance.match
        var query: String?

        NativeFeatureFlagRouteInvalidation.clearIfNeeded(
            entry: &entry,
            match: &match,
            query: &query,
            featureFlags: ["fediverse": false]
        )
        XCTAssertNil(entry)
        XCTAssertNil(match)

        let topic = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift"))
        XCTAssertFalse(topic.match.requiresFediverseFeature)
        entry = topic.entry
        match = topic.match
        NativeFeatureFlagRouteInvalidation.clearIfNeeded(
            entry: &entry,
            match: &match,
            query: &query,
            featureFlags: ["fediverse": false]
        )
        XCTAssertEqual(entry?.destinationIdentifier, .topicDetail)
        XCTAssertNotNil(match)
    }

    private func makeViewModel(
        state: FeatureFlagState,
        canOverride: Bool = true
    ) -> NativeFeatureFlagOverridesViewModel {
        NativeFeatureFlagOverridesViewModel(
            client: makeClient(),
            featureFlags: state,
            canOverride: canOverride
        )
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: .init(baseURL: URL(string: "https://example.test")!),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
    }

    private func makeState() -> FeatureFlagState {
        let fileURL = FileManager.default.temporaryDirectory
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
            .appendingPathComponent("feature-flags.json")
        return FeatureFlagState(store: FeatureFlagOverrideFileStore(fileURL: fileURL))
    }

    private func configureFeatureFlags() {
        CannedFeedURLProtocol.handlers["/api/v1/feature-flags"] = (
            ApiFixtureLoader.data("native.feature-flags.default"), 200
        )
    }

    private func resetProtocol() {
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.contentTypes = [:]
        CannedFeedURLProtocol.errors = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }
}

private struct FeatureFlagOverrideConsumerHarness: View {
    let viewModel: NativeFeatureFlagOverridesViewModel
    let state: FeatureFlagState

    var body: some View {
        VStack {
            NativeFeatureFlagOverridesSurface(viewModel: viewModel)
            NativeFeatureFlagAwareDirectoryView(
                section: .discover,
                isSignedIn: true,
                userRoles: [],
                canCastPublicVotes: true,
                featureFlags: state
            )
        }
    }
}

private struct FailingFeatureFlagOverrideStore: FeatureFlagOverridePersisting {
    enum Failure: Error { case write }

    func load() -> [String: Bool] {
        [:]
    }

    func set(_: Bool, for _: String) throws {
        throw Failure.write
    }

    func remove(_: String) throws {
        throw Failure.write
    }

    func clear() throws {
        throw Failure.write
    }
}
