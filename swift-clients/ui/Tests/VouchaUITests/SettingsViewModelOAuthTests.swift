import Foundation
import ViewInspector
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SettingsViewModelOAuthTests: NativeRouteSurfaceViewModelTestCase {
    func testCapabilitiesEnableConnectionAndBeginOpensAuthorizationURL() async throws {
        let fixture = try makeFixture()
        seedCapabilities()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/github/authorizations"] = (
            Data("""
            {
              "flow_id": "flow-1",
              "redirect_url": "https://github.com/login/oauth/authorize",
              "expires_at": "2027-01-01T00:05:00.000Z"
            }
            """.utf8),
            200
        )

        await fixture.viewModel.loadOAuthCapabilities()

        XCTAssertTrue(fixture.viewModel.canConnectOAuthAccount(provider: .github))
        XCTAssertFalse(fixture.viewModel.canConnectOAuthAccount(provider: .x))
        var openedURL: URL?
        await fixture.viewModel.beginOAuthConnection(provider: .github) { openedURL = $0 }

        XCTAssertEqual(openedURL?.host, "github.com")
        XCTAssertEqual(fixture.coordinator.pending?.flowId, "flow-1")
        XCTAssertFalse(fixture.viewModel.canConnectOAuthAccount(provider: .github))
        XCTAssertTrue(fixture.viewModel.canCancelOAuthAuthorization)
        XCTAssertNoThrow(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAuthorizationRecoveryControls.inspect().find(button: "Cancel"))
        XCTAssertNil(fixture.viewModel.oauthProviderInFlight)
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
    }

    func testBeginFailureAndMissingCoordinatorAreSafe() async throws {
        let fixture = try makeFixture()
        seedCapabilities()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/github/authorizations"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )

        await fixture.viewModel.beginOAuthConnection(provider: .github) { _ in
            XCTFail("A failed authorization must not open a URL")
        }

        XCTAssertNotNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertNil(fixture.viewModel.oauthProviderInFlight)

        let detached = SettingsViewModel(client: nil)
        await detached.beginOAuthConnection(provider: .facebook) { _ in
            XCTFail("A detached settings model cannot begin authorization")
        }
        await detached.disconnectOAuthAccount(provider: .facebook)
        XCTAssertFalse(detached.canConnectOAuthAccount(provider: .facebook))
    }

    func testCapabilityLoadFailureRendersErrorAndCanRetry() async throws {
        let fixture = try makeFixture()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )

        await fixture.viewModel.loadOAuthCapabilities()

        XCTAssertNotNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertTrue(fixture.viewModel.canRetryOAuthCapabilities)
        XCTAssertNoThrow(try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAuthorizationRecoveryControls.inspect().find(button: "Try Again"))

        seedCapabilities()
        await fixture.viewModel.retryOAuthCapabilityLoading()

        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertFalse(fixture.viewModel.canRetryOAuthCapabilities)
        XCTAssertTrue(fixture.viewModel.canConnectOAuthAccount(provider: .github))
    }

    func testAccountMappingSurfaceAndDisconnectSuccess() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let fixture = try makeFixture()
        await fixture.viewModel.load()
        seedCapabilities()
        await fixture.viewModel.loadOAuthCapabilities()
        fixture.viewModel.oauthErrorMessage = .verbatim("Connection failed")

        XCTAssertEqual(fixture.viewModel.account(for: .facebook)?.name, "Facebook Alice")
        XCTAssertEqual(fixture.viewModel.account(for: .x)?.name, "X Alice")
        XCTAssertEqual(fixture.viewModel.account(for: .github)?.emailAddress, "alice@github.example")
        let surface = SettingsSurface(viewModel: fixture.viewModel)
        XCTAssertNoThrow(try surface.inspect().find(text: "Facebook Alice"))
        XCTAssertNoThrow(try surface.inspect().find(text: "alice@github.example"))
        XCTAssertNoThrow(try surface.inspect().find(text: "Connection failed"))
        XCTAssertNoThrow(try surface.inspect().find(button: "Disconnect"))

        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/github/connect"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"message":"unrelated settings failure"}"#.utf8),
            503
        )
        await fixture.viewModel.disconnectOAuthAccount(provider: .github)

        XCTAssertNil(fixture.viewModel.account(for: .github))
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods.last {
            $0 == "DELETE"
        }, "DELETE")
    }

    func testDisconnectSurfacesIdentityRefreshAndConfirmationFailures() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let refreshFailure = try makeFixture()
        await refreshFailure.viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/github/connect"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )

        await refreshFailure.viewModel.disconnectOAuthAccount(provider: .github)

        XCTAssertNotNil(refreshFailure.viewModel.oauthErrorMessage)
        XCTAssertNotNil(refreshFailure.viewModel.account(for: .github))

        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let confirmationFailure = try makeFixture()
        await confirmationFailure.viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/github/connect"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )

        await confirmationFailure.viewModel.disconnectOAuthAccount(provider: .github)

        XCTAssertEqual(
            confirmationFailure.viewModel.oauthErrorMessage,
            .verbatim(NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed.localizedDescription)
        )
        XCTAssertNotNil(confirmationFailure.viewModel.account(for: .github))
    }

    func testDisconnectFailureAndConnectedResultConsumption() async throws {
        seedSettingsResponses()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            identityWithOAuthAccounts(),
            200
        )
        let fixture = try makeFixture()
        await fixture.viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/facebook/connect"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )

        await fixture.viewModel.disconnectOAuthAccount(provider: .facebook)

        XCTAssertEqual(fixture.viewModel.account(for: .facebook)?.name, "Facebook Alice")
        XCTAssertNotNil(fixture.viewModel.oauthErrorMessage)
        await fixture.viewModel.consumeOAuthAuthorizationResult(.authenticated(provider: .github))
        XCTAssertEqual(fixture.viewModel.account(for: .facebook)?.name, "Facebook Alice")

        fixture.coordinator.result = .connected(provider: .github)
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: [
                "github_account": oauthAccount(id: "github-2", name: "New GitHub", email: nil)
            ]),
            200
        )
        await fixture.viewModel.consumeOAuthAuthorizationResult(fixture.coordinator.result)

        XCTAssertEqual(fixture.viewModel.account(for: .github)?.name, "New GitHub")
        XCTAssertNil(fixture.coordinator.result)
    }

    func testConnectedResultWaitsForRefreshedConfirmedIdentity() async throws {
        let fixture = try makeFixture()
        try fixture.coordinator.store.record(.connected(provider: .github))
        fixture.coordinator.synchronizeFromStore()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )

        await fixture.viewModel.consumeOAuthAuthorizationResult(fixture.coordinator.result)

        XCTAssertEqual(fixture.coordinator.result, .connected(provider: .github))
        XCTAssertNil(fixture.viewModel.account(for: .github))
        XCTAssertEqual(
            fixture.viewModel.oauthErrorMessage,
            .verbatim(NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed.localizedDescription)
        )
        XCTAssertTrue(fixture.viewModel.canRetryOAuthResultConsumption)
        XCTAssertFalse(fixture.viewModel.canConnectOAuthAccount(provider: .github))

        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(overrides: [
                "github_account": oauthAccount(id: "github-2", name: "New GitHub", email: nil)
            ]),
            200
        )
        let surface = SettingsSurface(viewModel: fixture.viewModel)
        let recovery = try surface.oauthAuthorizationRecoveryControls.inspect()
        XCTAssertNoThrow(try recovery.find(button: "Cancel"))
        try recovery.find(button: "Try Again").tap()
        for _ in 0 ..< 500 where fixture.coordinator.result != nil {
            await Task.yield()
        }

        XCTAssertEqual(fixture.viewModel.account(for: .github)?.name, "New GitHub")
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertFalse(fixture.viewModel.canRetryOAuthResultConsumption)
    }

    func testConnectedResultRecoveryCanBeCancelledInPlace() async throws {
        let fixture = try makeFixture()
        try fixture.coordinator.store.record(.connected(provider: .github))
        fixture.coordinator.synchronizeFromStore()
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(),
            200
        )
        await fixture.viewModel.consumeOAuthAuthorizationResult(fixture.coordinator.result)
        let recovery = try SettingsSurface(viewModel: fixture.viewModel)
            .oauthAuthorizationRecoveryControls.inspect()

        try recovery.find(button: "Cancel").tap()

        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertFalse(fixture.viewModel.canRetryOAuthResultConsumption)
    }

    func testConnectedResultRetryTargetsFailedAcknowledgementBeforeIdentityRefresh() async throws {
        let fixture = try makeFixture()
        try fixture.coordinator.store.record(.connected(provider: .github))
        fixture.coordinator.synchronizeFromStore()
        fixture.coordinator.resultAcknowledgementNeedsRetry = true
        fixture.coordinator.errorMessage = NativeOAuthSecureStateError.unavailable.localizedDescription
        fixture.viewModel.synchronizeOAuthError()

        await fixture.viewModel.retryOAuthResultConsumption()

        XCTAssertNil(fixture.coordinator.result)
        XCTAssertFalse(fixture.coordinator.resultAcknowledgementNeedsRetry)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/my/identity"), 0)
    }

    func testExpiredConnectionResultRemainsVisibleUntilDismissed() async throws {
        let fixture = try makeFixture()
        seedCapabilities()
        await fixture.viewModel.loadOAuthCapabilities()
        try fixture.coordinator.store.record(.expired(
            provider: .github,
            purpose: .connect
        ))
        fixture.coordinator.synchronizeFromStore()

        await fixture.viewModel.consumeOAuthAuthorizationResult(fixture.coordinator.result)

        XCTAssertEqual(
            fixture.coordinator.result,
            .expired(provider: .github, purpose: .connect)
        )
        XCTAssertEqual(
            fixture.viewModel.oauthErrorMessage,
            .message(.nativeSwiftSettingsOauthExpired)
        )
        XCTAssertFalse(fixture.viewModel.canConnectOAuthAccount(provider: .github))
        let surface = SettingsSurface(viewModel: fixture.viewModel)
        XCTAssertNoThrow(try surface.oauthAccountsSection.inspect()
            .find(text: "Authorization expired. Try again."))
        XCTAssertNoThrow(try surface.oauthAuthorizationRecoveryControls.inspect().find(button: "OK"))

        fixture.viewModel.dismissOAuthExpiration()

        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
        XCTAssertTrue(fixture.viewModel.canConnectOAuthAccount(provider: .github))
    }

    func testFailedFinalizationRendersRetryAndCancelRecoveryControls() throws {
        let fixture = try makeFixture()
        let now = Date()
        XCTAssertTrue(fixture.coordinator.store.save(
            flowId: "flow-recovery",
            provider: .github,
            purpose: .connect,
            completionProofVerifier: "verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=flow-recovery&completion_token=token"
        ))
        XCTAssertNotNil(try fixture.coordinator.store.claimCallback(for: callback, now: now))
        fixture.coordinator.synchronizeFromStore()
        fixture.coordinator.errorMessage = "Connection failed"

        fixture.viewModel.synchronizeOAuthError()
        let surface = SettingsSurface(viewModel: fixture.viewModel)

        XCTAssertTrue(fixture.viewModel.canRecoverOAuthFinalization)
        XCTAssertNoThrow(try surface.oauthAccountsSection.inspect().find(text: "Connection failed"))
        XCTAssertNoThrow(try surface.oauthAuthorizationRecoveryControls.inspect().find(button: "Try Again"))
        XCTAssertNoThrow(try surface.oauthAuthorizationRecoveryControls.inspect().find(button: "Cancel"))

        fixture.viewModel.cancelOAuthAuthorization()
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.viewModel.oauthErrorMessage)
    }

    private func makeFixture() throws -> (
        viewModel: SettingsViewModel,
        coordinator: NativeOAuthAuthorizationCoordinator
    ) {
        let client = try makeClient()
        let coordinator = try NativeOAuthAuthorizationCoordinator(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage()),
            store: NativeOAuthAuthorizationStore(
                secureState: UserDefaultsNativePendingState(
                    key: "nativeOAuthPendingAuthorization",
                    defaults: XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
                )
            )
        )
        return (
            SettingsViewModel(
                client: client,
                nativeOAuthAuthorizationCoordinator: coordinator
            ),
            coordinator
        )
    }

    private func seedCapabilities() {
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            Data("""
            {
              "providers": ["facebook", "x", "github"],
              "broker_capabilities": {
                "facebook": {
                  "version": 1,
                  "modes": {"web": true, "native": true},
                  "purposes": ["authenticate", "connect"]
                },
                "x": {
                  "version": 1,
                  "modes": {"web": true, "native": false},
                  "purposes": ["authenticate", "connect"]
                },
                "github": {
                  "version": 1,
                  "modes": {"web": true, "native": true},
                  "purposes": ["authenticate", "connect"]
                }
              }
            }
            """.utf8),
            200
        )
    }

    private func identityWithOAuthAccounts() -> Data {
        PrivateUserTestFixture.identityEnvelope(overrides: [
            "facebook_account": oauthAccount(
                id: "facebook-1",
                name: "Facebook Alice",
                email: nil
            ),
            "x_account": oauthAccount(id: "x-1", name: "X Alice", email: nil),
            "github_account": oauthAccount(
                id: "github-1",
                name: "GitHub Alice",
                email: "alice@github.example"
            )
        ])
    }

    private func oauthAccount(id: String, name: String, email: String?) -> [String: Any] {
        [
            "id": id,
            "name": name,
            "email_address": email.map { $0 as Any } ?? NSNull()
        ]
    }
}

extension SettingsViewModelOAuthTests: CompleteSettingsResponseSeeding {}
