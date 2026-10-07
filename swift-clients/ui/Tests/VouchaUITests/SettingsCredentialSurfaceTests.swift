import Foundation
import Observation
import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class SettingsCredentialSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testStagedCatalogRendersLocalizedUmbrellaMeaningWithoutSelectingResourceScopes() async throws {
        seedCredentials()
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (ApiFixtureLoader.data("shared.scopes.catalog"), 200)
        let model = try SettingsViewModel(client: makeClient())
        model.apiKeyType = .mcp
        await model.loadCredentialSettings()
        let surface = SettingsSurface(viewModel: model)
        XCTAssertNoThrow(try surface.apiKeyScopePicker.inspect()
            .find(text: uiEnglish(.nativeCredentialsMcpUserFullAccess)))
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
        XCTAssertFalse(try surface.apiKeysSection.inspect().find(button: "Create API Key").isDisabled())
    }

    func testConnectedGrantRevokeButtonCallsOwnedEndpointAndRemovesAfterCompletion() async throws {
        seedCredentials()
        let model = try SettingsViewModel(client: makeClient())
        await model.loadCredentialSettings()
        let path = "/api/v1/my/oauth-grants/one"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "DELETE")
        let surface = SettingsSurface(viewModel: model)
        try surface.connectedAppsSection.inspect().find(button: "Revoke").tap()
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

    private func seedCredentials() {
        CannedFeedURLProtocol.handlers["/api/v1/scopes"] = (SettingsCredentialsTestData.catalog, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/oauth-grants"] = (SettingsCredentialsTestData.grants(["one"]), 200)
    }
}
