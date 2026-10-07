import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SettingsCredentialLoadingIsolationTests: NativeRouteSurfaceViewModelTestCase,
    CompleteSettingsResponseSeeding {
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
}
