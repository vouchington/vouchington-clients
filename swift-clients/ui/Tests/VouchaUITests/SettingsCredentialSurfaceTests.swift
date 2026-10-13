import Foundation
import Observation
import ViewInspector
@testable import VouchaAPI
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class SettingsCredentialSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testApiKeySectionRendersExpiryStatusAndRotateActionFromCanonicalFixture() throws {
        let model = SettingsViewModel(client: nil)
        let response = try APIClient.makeDecoder().decode(
            SettingsApiKeyResponse.self,
            from: ApiFixtureLoader.data("native.my.api-keys.rotate")
        )
        model.apiKeyPagination.reset(items: [response.apiKey])
        let owner = try APIClient.makeDecoder().decode(
            SettingsIdentityResponse.self, from: PrivateUserTestFixture.identityEnvelope()
        )
        model.apply(identity: owner.identity)
        let surface = SettingsSurface(viewModel: model)
        let section = try surface.apiKeysSection.inspect()

        XCTAssertNoThrow(try section.find(text: uiEnglish(.nativeApiKeysLifetimeLabel)))
        XCTAssertNoThrow(try section.find(text: uiEnglish(.nativeApiKeysLifetime90)))
        XCTAssertNoThrow(try section.find(button: uiEnglish(.nativeApiKeysRotate)))
        XCTAssertNoThrow(try section.find(ViewType.Text.self, where: {
            try $0.string().contains("Expires")
        }))
    }

    func testStagedCatalogRendersLocalizedUmbrellaMeaningWithoutSelectingResourceScopes() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (ApiFixtureLoader.data("shared.scopes.catalog"), 200)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        await model.loadCredentialSettings()
        let surface = SettingsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect()
            .find(text: uiEnglish(.nativeCredentialsMcpUserFullAccess)))
        for description in [
            UiMessageKey.nativeCredentialsFinancialProfileRead,
            .nativeCredentialsFinancialProfileWrite,
            .nativeCredentialsSpendingRead,
            .nativeCredentialsSpendingWrite
        ] {
            XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: uiEnglish(description)))
        }
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "financial-profile:read"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "spending:read"))
        XCTAssertFalse(model.apiKeyScopes.contains { $0.descriptionKey == .mcpAdminFullAccess })
        let renderedText = try surface.apiKeyScopePicker.inspect().findAll(ViewType.Text.self).map { try $0.string() }
        XCTAssertFalse(renderedText.contains(uiEnglish(.nativeCredentialsMcpAdminFullAccess)))
        let umbrella = try XCTUnwrap(model.apiKeyScopes
            .first { $0.descriptionKey == .mcpUserFullAccess && $0.action == .read })
        model.setApiKeyScope(umbrella.scope, selected: true)
        XCTAssertEqual(model.apiKeyScopeSelection.permissions, [umbrella.scope])
    }

    func testScopePickerShowsCatalogueMetadataAndCreatesOnlyAfterSelection() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        let surface = SettingsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "data:read"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "data:write"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "Requires data:read"))
        XCTAssertThrowsError(try surface.apiKeyScopePicker.inspect().find(text: "feed:read"))
        let create = try surface.apiKeysSection.inspect().find(button: "Create API Key")
        XCTAssertTrue(try create.isDisabled())
        let toggle = try surface.apiKeyScopePicker.inspect().find(ViewType.Toggle.self, where: {
            try $0.accessibilityIdentifier() == "api-key-scope-data:write"
        })
        try toggle.tap()
        XCTAssertEqual(model.apiKeyScopeSelection.permissions, ["data:read", "data:write"])
        XCTAssertTrue(try surface.apiKeysSection.inspect().find(button: "Create API Key").isDisabled())
        let owner = try APIClient.makeDecoder().decode(
            SettingsIdentityResponse.self, from: PrivateUserTestFixture.identityEnvelope()
        )
        model.apply(identity: owner.identity)
        XCTAssertFalse(try surface.apiKeysSection.inspect().find(button: "Create API Key").isDisabled())
    }

    func testConnectedAppsShowsClientResourceScopesAndActivity() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let surface = SettingsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(text: "Agent one"))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(text: "Verified"))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(text: "https://voucha.ai/api/v1/mcp"))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(text: "data:read"))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(button: "Revoke"))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(ViewType.Text.self, where: {
            try $0.string().contains("Not used yet")
        }))
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (
            SettingsCredentialsTestData.grants(["one"], lastUsed: true),
            200
        )
        await model.loadCredentialSettings()
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(ViewType.Text.self, where: {
            try $0.string().contains("Last used")
        }))
    }

    func testCredentialPickerDisplaysLoadingAndRetryableFailure() throws {
        let model = SettingsViewModel(client: nil)
        let surface = SettingsSurface(viewModel: model)
        model.credentialState = .loading
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(ViewType.ProgressView.self))
        model.credentialState = .error(.api(statusCode: 503, preconditionCode: "CATALOG_DOWN"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "API error: CATALOG_DOWN"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(button: "Try Again"))
        XCTAssertFalse(model.canCreateApiKey)
    }

    func testGrantFailureAppearsInConnectedAppsWithoutBlockingScopePicker() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (Data(#"{"error":"unauthorized"}"#.utf8), 401)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        let surface = SettingsSurface(viewModel: model)

        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(text: "Sign in to continue."))
        XCTAssertNoThrow(try surface.connectedAppsSection.inspect().find(button: "Try Again"))
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect().find(text: "data:write"))
        XCTAssertThrowsError(try surface.apiKeyScopePicker.inspect().find(text: "Sign in to continue."))
        model.setApiKeyScope("data:write", selected: true)
        let owner = try APIClient.makeDecoder().decode(
            SettingsIdentityResponse.self, from: PrivateUserTestFixture.identityEnvelope()
        )
        model.apply(identity: owner.identity)
        XCTAssertFalse(try surface.apiKeysSection.inspect().find(button: "Create API Key").isDisabled())
    }

    func testConnectedAppsRetryReloadsOnlyGrantsAndPreservesValidScopes() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (Data(#"{"error":"unauthorized"}"#.utf8), 401)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        model.apiKeyLabel = "Agent"
        await model.loadCredentialSettings()
        model.setApiKeyScope("data:write", selected: true)
        let owner = try APIClient.makeDecoder().decode(
            SettingsIdentityResponse.self, from: PrivateUserTestFixture.identityEnvelope()
        )
        model.apply(identity: owner.identity)
        XCTAssertTrue(model.canCreateApiKey)
        let scopeRequests = CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == "/api/v1/scopes" }.count

        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (Data(#"{"error":"offline"}"#.utf8), 503)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants(["two"]), 200)
        let surface = SettingsSurface(viewModel: model)
        let retry = try surface.connectedAppsSection.inspect().find(ViewType.View<ErrorStateView>.self)
            .actualView().retry
        await retry()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == "/api/v1/scopes" }.count,
            scopeRequests
        )
        guard case .loaded = model.credentialState else { return XCTFail("Valid scope catalog must remain loaded") }
        guard case .loaded = model.oauthGrantState else { return XCTFail("Grant retry must recover") }
        XCTAssertEqual(model.oauthGrants.map(\.id), ["two"])
        XCTAssertEqual(model.apiKeyScopeSelection.permissions, ["data:read", "data:write"])
        XCTAssertTrue(model.canCreateApiKey)
    }

    func testScopePickerRetryReloadsOnlyCatalogAndPreservesValidGrants() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (Data(#"{"error":"offline"}"#.utf8), 503)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        await model.loadCredentialSettings()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        let grantRequests = CannedFeedURLProtocol.capturedRequests
            .filter { $0.url.path == "/api/v1/my/oauth-grants" }.count

        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (Data(#"{"error":"offline"}"#.utf8), 503)
        let surface = SettingsSurface(viewModel: model)
        let retry = try surface.apiKeyScopePicker.inspect().find(ViewType.View<ErrorStateView>.self)
            .actualView().retry
        await retry()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedRequests
                .filter { $0.url.path == "/api/v1/my/oauth-grants" }.count,
            grantRequests
        )
        guard case .loaded = model.credentialState else { return XCTFail("Scope retry must recover") }
        guard case .loaded = model.oauthGrantState else { return XCTFail("Valid grants must remain loaded") }
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        XCTAssertTrue(model.apiKeyScopes.contains { $0.scope == "data:write" })
    }

    func testConnectedGrantRevokeRequiresNamedConfirmationAndCancelMakesNoRequest() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let path = "/api/v1/my/oauth-grants/one"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "DELETE")
        let grant = try XCTUnwrap(model.oauthGrants.first)
        let state = OAuthGrantRevokeInteractionState()
        let control = OAuthGrantRevokeButton(
            grant: grant,
            locale: Locale(identifier: "en_US"),
            isDisabled: false,
            interactionState: state
        ) { await model.revokeOAuthGrant(id: grant.id) }
        try control.inspect().find(button: "Revoke").tap()
        XCTAssertTrue(state.confirming)
        let dialog = try control.inspect().find(ViewType.Button.self).confirmationDialog()
        XCTAssertEqual(try dialog.title().string(), "Revoke access to Agent one?")
        try dialog.actions().find(button: "Cancel").tap()
        XCTAssertFalse(state.confirming)
        XCTAssertFalse(CannedFeedURLProtocol.capturedRequests.contains {
            $0.url.path == path && $0.method == "DELETE"
        })
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])

        try control.inspect().find(button: "Revoke").tap()
        XCTAssertTrue(state.confirming)
        let confirmation = try control.inspect().find(ViewType.Button.self).confirmationDialog()
        try confirmation.actions().find(button: "Revoke").tap()
        _ = try await barrier.wait()
        XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        let completed = expectation(description: "Successful revoke updates the observed list")
        withObservationTracking {
            _ = model.oauthGrants
        } onChange: {
            completed.fulfill()
        }
        CannedFeedURLProtocol.releaseResponse(path: path)
        await fulfillment(of: [completed], timeout: 2)
        XCTAssertTrue(model.oauthGrants.isEmpty)
    }

    func testConnectedAppsProductControlWiresOwnedGrantRevoke() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let section = SettingsSurface(viewModel: model).connectedAppsSection
        XCTAssertNoThrow(try section.inspect().find(text: "Agent one"))
        let control = try section.inspect().find(ViewType.View<OAuthGrantRevokeButton>.self).actualView()
        XCTAssertEqual(control.grant.id, "one")

        let path = "/api/v1/my/oauth-grants/one"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "DELETE")
        let revoke = Task { await control.revoke() }
        do {
            _ = try await barrier.wait(timeout: .seconds(5))
            XCTAssertEqual(model.oauthGrants.map(\.id), ["one"])
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            await revoke.value
            throw error
        }
        CannedFeedURLProtocol.releaseResponse(path: path)
        await revoke.value
        XCTAssertTrue(model.oauthGrants.isEmpty)
    }

    private func seedCredentials() {
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants(["one"]), 200)
    }
}
