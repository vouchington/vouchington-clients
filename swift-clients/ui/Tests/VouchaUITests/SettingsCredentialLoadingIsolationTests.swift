import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SettingsCredentialLoadingIsolationTests: NativeRouteSurfaceViewModelTestCase,
    CompleteSettingsResponseSeeding {
    func testRevokedKeyStaysRemovedWhileUnrelatedMainSettingsFinishLoading() async throws {
        seedSettingsResponses()
        let model = try SettingsViewModel(client: makeClient())
        await model.load()
        XCTAssertEqual(model.apiKeys.map(\.id), ["key-1"])
        XCTAssertEqual(model.profileMarkdown, "Native bio")
        XCTAssertEqual(model.profileLinks.map(\.id), ["link-1"])

        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Updated bio"}}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data(#"{"results":[],"page_info":{"has_next_page":false,"start_cursor":null,"end_cursor":null}}"#.utf8),
            200
        )

        let linksPath = "/api/v1/my/profile/links"
        let keysPath = "/api/v1/my/api-keys"
        CannedFeedURLProtocol.suspendResponse(path: linksPath)
        CannedFeedURLProtocol.suspendResponse(path: keysPath)
        let linksRequest = CannedFeedURLProtocol.requestBarrier(path: linksPath, method: "GET")
        let keysRequest = CannedFeedURLProtocol.requestBarrier(path: keysPath, method: "GET")
        let reload = Task { await model.reload() }
        do {
            _ = try await linksRequest.wait()
            _ = try await keysRequest.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: linksPath)
            CannedFeedURLProtocol.releaseResponse(path: keysPath)
            await reload.value
            throw error
        }

        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys/key-1"] = (Data(), 204)
        await model.revokeApiKey(id: "key-1")
        XCTAssertTrue(model.apiKeys.isEmpty)
        XCTAssertTrue(model.isLoading, "The unrelated main settings requests are still held")

        CannedFeedURLProtocol.releaseResponse(path: keysPath)
        CannedFeedURLProtocol.releaseResponse(path: linksPath)
        await reload.value

        guard case .loaded = model.state else { return XCTFail("The main settings load must finish") }
        XCTAssertEqual(model.profileMarkdown, "Updated bio")
        XCTAssertTrue(model.profileLinks.isEmpty)
        XCTAssertTrue(model.apiKeys.isEmpty, "The stale key response must not restore a revoked key")
    }

    func testApiKeyCreationUsesLoadedCatalogWhileProfileLinksAreHeld() async throws {
        seedSettingsResponses()
        let linksPath = "/api/v1/my/profile/links"
        let apiKeysPath = "/api/v1/my/api-keys"
        CannedFeedURLProtocol.suspendResponse(path: linksPath)
        CannedFeedURLProtocol.suspendResponse(path: apiKeysPath)
        let linksRequest = CannedFeedURLProtocol.requestBarrier(path: linksPath, method: "GET")
        let apiKeysRequest = CannedFeedURLProtocol.requestBarrier(path: apiKeysPath, method: "GET")
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        let load = Task { await model.load() }
        do {
            _ = try await linksRequest.wait()
            _ = try await apiKeysRequest.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: linksPath)
            CannedFeedURLProtocol.releaseResponse(path: apiKeysPath)
            await load.value
            throw error
        }
        CannedFeedURLProtocol.releaseResponse(path: apiKeysPath)
        await assertEventuallySettingsState(model) {
            if case .loaded = model.credentialState { return true }
            return false
        }
        model.setApiKeyScope("data:write", selected: true)
        XCTAssertTrue(model.isLoading)
        XCTAssertTrue(model.canCreateApiKey)
        let surface = SettingsSurface(viewModel: model)
        XCTAssertEqual(try? surface.apiKeysSection.inspect().find(button: "Create API Key").isDisabled(), false)

        CannedFeedURLProtocol.handlers[apiKeysPath] = (ApiFixtureLoader.data("native.my.api-keys.create"), 201)
        await model.createApiKey()
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.contains {
            $0.url.path == "/api/v1/my/api-keys" && $0.method == "POST"
        })
        XCTAssertEqual(model.latestRawAPIKey, "fixture-raw-api-key")
        XCTAssertTrue(model.isLoading, "The unrelated settings batch is still awaiting profile links")
        CannedFeedURLProtocol.releaseResponse(path: linksPath)
        await load.value
        guard case .loaded = model.state else { return XCTFail("The main settings load must finish") }
        XCTAssertEqual(model.profileMarkdown, "Native bio")
        XCTAssertEqual(model.profileLinks.map(\.id), ["link-1"])
        XCTAssertEqual(model.apiKeys.map(\.id), ["00000000-0000-7000-8000-000000000701", "key-1"])
    }

    func testApiKeyCreationDoesNotReopenMainLoadingWhileGrantsAreHeld() async throws {
        seedSettingsResponses()
        let grantsPath = "/api/v1/my/oauth-grants"
        CannedFeedURLProtocol.suspendResponse(path: grantsPath)
        let grantsRequest = CannedFeedURLProtocol.requestBarrier(path: grantsPath, method: "GET")
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        let load = Task { await model.load() }
        do {
            _ = try await grantsRequest.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: grantsPath)
            await load.value
            throw error
        }
        await assertEventuallySettingsState(model) {
            if case .loaded = model.state, case .loaded = model.credentialState { return true }
            return false
        }
        model.setApiKeyScope("data:write", selected: true)
        XCTAssertTrue(model.canCreateApiKey)
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (
            ApiFixtureLoader.data("native.my.api-keys.create"), 201
        )
        await model.createApiKey()

        if case .loaded = model.state {} else { XCTFail("Held grants must not reopen main loading") }
        if case .loading = model.oauthGrantState {} else { XCTFail("Grants should remain held") }
        XCTAssertEqual(model.latestRawAPIKey, "fixture-raw-api-key")
        CannedFeedURLProtocol.releaseResponse(path: grantsPath)
        await load.value
    }

    func testUnrelatedSettingsFailuresStillLoadCredentialsForFreshIdentity() async throws {
        for failingPath in [
            "/api/v1/my/profile", "/api/v1/my/profile/links", "/api/v1/my/api-keys",
            "/api/v1/my/notifications/push-subscriptions", "/api/v1/auth/sessions"
        ] {
            seedSettingsResponses()
            CannedFeedURLProtocol.handlers[failingPath] = (Data("{".utf8), 200)
            let model = try SettingsViewModel(client: makeClient())
            model.apiKeyType = .mcp
            model.apiKeyLabel = "Agent"

            await model.load()

            XCTAssertNotNil(model.identity, failingPath)
            guard case .error = model.state else {
                return XCTFail("Settings failure must remain visible: \(failingPath)")
            }
            guard case .loaded = model.credentialState else { return XCTFail("Catalog skipped: \(failingPath)") }
            guard case .loaded = model.oauthGrantState else { return XCTFail("Grants skipped: \(failingPath)") }
            XCTAssertTrue(model.apiKeyScopes.contains { $0.scope == "data:write" }, failingPath)
            model.setApiKeyScope("data:write", selected: true)
            XCTAssertTrue(model.canCreateApiKey, failingPath)
        }
    }

    func testFailedIdentityReloadDiscardsPriorAdministratorScopes() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(roles: ["administrator"]), 200
        )
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Admin agent"
        await model.load()
        XCTAssertEqual(model.identity?.roles, ["administrator"])
        model.setApiKeyScope("data:write", selected: true)
        XCTAssertTrue(model.canCreateApiKey)

        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (Data("{".utf8), 200)
        await model.reload()

        guard case .error = model.state else { return XCTFail("Identity failure must remain visible") }
        XCTAssertNil(model.identity)
        XCTAssertTrue(model.apiKeyScopes.isEmpty)
        XCTAssertTrue(model.apiKeyScopeSelection.permissions.isEmpty)
        XCTAssertFalse(model.canCreateApiKey)
        guard case .idle = model.credentialState else { return XCTFail("Old catalog must be invalidated") }
        guard case .idle = model.oauthGrantState else { return XCTFail("Old grants must be invalidated") }
        XCTAssertTrue(model.oauthGrants.isEmpty)
    }

    func testMainSettingsLoadProgressesWhileCredentialRequestsAreHeld() async throws {
        seedSettingsResponses()
        let scopePath = "/api/v1/scopes"
        let grantsPath = "/api/v1/my/oauth-grants"
        let profilePath = "/api/v1/my/profile"
        CannedFeedURLProtocol.suspendResponse(path: scopePath)
        CannedFeedURLProtocol.suspendResponse(path: grantsPath)
        CannedFeedURLProtocol.suspendResponse(path: profilePath)
        let scopeRequest = CannedFeedURLProtocol.requestBarrier(path: scopePath, method: "GET")
        let grantsRequest = CannedFeedURLProtocol.requestBarrier(path: grantsPath, method: "GET")
        let profileRequest = CannedFeedURLProtocol.requestBarrier(path: profilePath, method: "GET")
        let model = try SettingsViewModel(client: makeClient())
        let load = Task { await model.load() }

        let scopeRequestStarted = await (try? scopeRequest.wait(timeout: .seconds(1))) != nil
        let grantsRequestStarted = await (try? grantsRequest.wait(timeout: .seconds(1))) != nil
        let credentialRequestsStarted = scopeRequestStarted && grantsRequestStarted
        let profileStarted = await (try? profileRequest.wait(timeout: .seconds(1))) != nil
        CannedFeedURLProtocol.releaseResponse(path: profilePath)
        if profileStarted {
            await assertEventuallySettingsState(model) {
                if case .loaded = model.state { return true }
                return false
            }
        }
        let mainSettingsLoaded: Bool = {
            if case .loaded = model.state { return true }
            return false
        }()
        let mainValuesApplied = model.profileMarkdown == "Native bio" && model.profileLinks.map(\.id) == ["link-1"] &&
            model.apiKeys.map(\.id) == ["key-1"] && model.pushSubscriptions.map(\.id) == ["sub-1"] &&
            model.sessions.map(\.id) == ["session-1"] && model.membership?.id == "mem-1" &&
            model.dataRequest?.id == "req-1"

        CannedFeedURLProtocol.releaseResponse(path: scopePath)
        CannedFeedURLProtocol.releaseResponse(path: grantsPath)
        await load.value

        XCTAssertTrue(credentialRequestsStarted, "Credential requests must remain active concurrently")
        XCTAssertTrue(profileStarted, "Profile loading must start before credential requests finish")
        XCTAssertTrue(mainSettingsLoaded, "Unrelated settings should load while credentials are pending")
        XCTAssertTrue(mainValuesApplied, "Completed main responses should be applied before credentials finish")
        guard case .loaded = model.credentialState else { return XCTFail("Scope catalog should finish after release") }
        guard case .loaded = model.oauthGrantState else { return XCTFail("OAuth grants should finish after release") }
    }

    func testMainSettingsFailureSurfacesWhileCredentialRequestsAreHeld() async throws {
        seedSettingsResponses()
        let scopePath = "/api/v1/scopes"
        let grantsPath = "/api/v1/my/oauth-grants"
        CannedFeedURLProtocol.suspendResponse(path: scopePath)
        CannedFeedURLProtocol.suspendResponse(path: grantsPath)
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (Data("{".utf8), 200)
        let scopeRequest = CannedFeedURLProtocol.requestBarrier(path: scopePath, method: "GET")
        let grantsRequest = CannedFeedURLProtocol.requestBarrier(path: grantsPath, method: "GET")
        let model = try SettingsViewModel(client: makeClient())
        let load = Task { await model.load() }

        let scopeRequestStarted = await (try? scopeRequest.wait(timeout: .seconds(1))) != nil
        let grantsRequestStarted = await (try? grantsRequest.wait(timeout: .seconds(1))) != nil
        if scopeRequestStarted, grantsRequestStarted {
            await assertEventuallySettingsState(model) {
                if case .error = model.state { return true }
                return false
            }
        }
        let mainFailurePublished = if case .error = model.state { true } else { false }
        let credentialsStillLoading = if case .loading = model.credentialState { true } else { false }

        CannedFeedURLProtocol.releaseResponse(path: scopePath)
        CannedFeedURLProtocol.releaseResponse(path: grantsPath)
        await load.value

        XCTAssertTrue(scopeRequestStarted && grantsRequestStarted, "Both credential requests must stay held")
        XCTAssertTrue(mainFailurePublished, "Main settings errors should surface before credentials finish")
        XCTAssertTrue(credentialsStillLoading, "Held credential requests should remain independently loading")
        guard case .loaded = model.credentialState else { return XCTFail("Catalog should finish after release") }
        guard case .loaded = model.oauthGrantState else { return XCTFail("Grants should finish after release") }
    }

    private func assertEventuallySettingsState(
        _ model: SettingsViewModel,
        matches predicate: @escaping @MainActor () -> Bool
    ) async {
        let reachedState = expectation(description: "Settings reaches expected state")
        func observe() {
            if predicate() {
                reachedState.fulfill()
                return
            }
            withObservationTracking {
                _ = model.state
                _ = model.credentialState
            } onChange: {
                Task { @MainActor in observe() }
            }
        }
        observe()
        await fulfillment(of: [reachedState], timeout: 1)
    }
}
