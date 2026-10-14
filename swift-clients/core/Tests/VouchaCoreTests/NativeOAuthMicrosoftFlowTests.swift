import Foundation
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaModels
import XCTest

@MainActor
extension NativeOAuthAuthorizationCoordinatorTests {
    func capabilitiesData() -> Data {
        Data("""
        {
          "providers": ["facebook", "x", "github", "linkedin", "microsoft"],
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
            },
            "microsoft": {
              "version": 1,
              "modes": {"web": true, "native": true},
              "purposes": ["authenticate", "connect"]
            },
            "linkedin": {
              "version": 1,
              "modes": {"web": true, "native": true},
              "purposes": ["authenticate", "connect"]
            }
          }
        }
        """.utf8)
    }

    func testMicrosoftBeginUsesNativeBrokerOnlyWhenAdvertised() async throws {
        let enabled = try makeFixture()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = [
            (capabilitiesData(), 200)
        ]
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/microsoft/authorizations"] = [
            (Data("""
            {
              "flow_id": "microsoft-flow",
              "redirect_url": "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
              "expires_at": "2027-01-01T00:05:00.000Z"
            }
            """.utf8), 200)
        ]
        var openedURL: URL?

        await enabled.coordinator.begin(provider: .microsoft, purpose: .authenticate) {
            openedURL = $0
        }

        XCTAssertEqual(openedURL?.host, "login.microsoftonline.com")
        XCTAssertEqual(enabled.coordinator.pending?.provider, .microsoft)
        XCTAssertEqual(
            OAuthCoordinatorURLProtocol.requests.last?.url?.path,
            "/api/v1/auth/oauth/microsoft/authorizations"
        )

        let disabled = try makeFixture()
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: capabilitiesData()) as? [String: Any]
        )
        var capabilities = try XCTUnwrap(response["broker_capabilities"] as? [String: Any])
        capabilities.removeValue(forKey: "microsoft")
        response["broker_capabilities"] = capabilities
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = try [
            (JSONSerialization.data(withJSONObject: response), 200)
        ]
        await disabled.coordinator.begin(provider: .microsoft, purpose: .authenticate) { _ in
            XCTFail("Missing Microsoft native capability must not open a URL")
        }
        XCTAssertNil(disabled.coordinator.pending)
        XCTAssertFalse(disabled.coordinator.supports(provider: .microsoft, purpose: .authenticate))
    }

    func testMicrosoftFinalizationConfirmsAuthenticationAndConnectedIdentity() async throws {
        let authenticated = try makeFixture()
        let authenticatedPending = try claim(
            store: authenticated.store,
            flowId: "microsoft-auth",
            provider: .microsoft,
            purpose: .authenticate,
            now: authenticated.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/microsoft-auth/complete"
        ] = [(Data("""
        {"user":{"id":"user-1","username":"alice","email_address":null,
        "roles":["user"],"profile_image_id":null}}
        """.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [(identityData(), 200)]

        await authenticated.coordinator.finalize(authenticatedPending)

        XCTAssertEqual(authenticated.coordinator.result, .authenticated(provider: .microsoft))
        XCTAssertTrue(authenticated.sessionManager.isSignedIn)

        let connected = try makeFixture()
        let connectedPending = try claim(
            store: connected.store,
            flowId: "microsoft-connect",
            provider: .microsoft,
            purpose: .connect,
            now: connected.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/microsoft-connect/complete"
        ] = [(Data(#"{"oauth_account":{"id":"microsoft-1","name":"Alice","email_address":null}}"#.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [
            (identityData(provider: .microsoft), 200)
        ]

        await connected.coordinator.finalize(connectedPending)

        XCTAssertEqual(connected.coordinator.result, .connected(provider: .microsoft))
        XCTAssertTrue(connected.sessionManager.isSignedIn)
    }

    func testMicrosoftFinalizationFailureCanRetryWithoutNewAuthorization() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "microsoft-retry",
            provider: .microsoft,
            purpose: .authenticate,
            now: fixture.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/microsoft-retry/complete"
        ] = [
            (Data(#"{"message":"offline"}"#.utf8), 503),
            (Data(#"{"mfa_required":true,"login_attempt_id":"microsoft-attempt"}"#.utf8), 200)
        ]

        await fixture.coordinator.finalize(pending)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .microsoft, loginAttemptId: "microsoft-attempt")
        )
        XCTAssertEqual(OAuthCoordinatorURLProtocol.requests.filter {
            $0.url?.path == "/api/v1/auth/oauth/authorizations/microsoft-retry/complete"
        }.count, 2)
    }
}
