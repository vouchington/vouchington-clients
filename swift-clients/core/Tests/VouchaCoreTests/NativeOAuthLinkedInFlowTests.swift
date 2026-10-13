import Foundation
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaModels
import XCTest

@MainActor
extension NativeOAuthAuthorizationCoordinatorTests {
    func testLinkedInBeginUsesNativeBrokerOnlyWhenAdvertised() async throws {
        let enabled = try makeFixture()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = [
            (capabilitiesData(), 200)
        ]
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/linkedin/authorizations"] = [
            (Data("""
            {
              "flow_id": "linkedin-flow",
              "redirect_url": "https://www.linkedin.com/oauth/v2/authorization",
              "expires_at": "2027-01-01T00:05:00.000Z"
            }
            """.utf8), 200)
        ]
        var openedURL: URL?

        await enabled.coordinator.begin(provider: .linkedin, purpose: .authenticate) {
            openedURL = $0
        }

        XCTAssertEqual(openedURL?.host, "www.linkedin.com")
        XCTAssertEqual(enabled.coordinator.pending?.provider, .linkedin)
        XCTAssertEqual(
            OAuthCoordinatorURLProtocol.requests.last?.url?.path,
            "/api/v1/auth/oauth/linkedin/authorizations"
        )

        let disabled = try makeFixture()
        var response = try XCTUnwrap(
            JSONSerialization.jsonObject(with: capabilitiesData()) as? [String: Any]
        )
        var capabilities = try XCTUnwrap(response["broker_capabilities"] as? [String: Any])
        capabilities.removeValue(forKey: "linkedin")
        response["broker_capabilities"] = capabilities
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = try [
            (JSONSerialization.data(withJSONObject: response), 200)
        ]
        await disabled.coordinator.begin(provider: .linkedin, purpose: .authenticate) { _ in
            XCTFail("Missing LinkedIn native capability must not open a URL")
        }
        XCTAssertNil(disabled.coordinator.pending)
        XCTAssertFalse(disabled.coordinator.supports(provider: .linkedin, purpose: .authenticate))
    }

    func testLinkedInFinalizationConfirmsAuthenticationAndConnectedIdentity() async throws {
        let authenticated = try makeFixture()
        let authenticatedPending = try claim(
            store: authenticated.store,
            flowId: "linkedin-auth",
            provider: .linkedin,
            purpose: .authenticate,
            now: authenticated.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/linkedin-auth/complete"
        ] = [(Data("""
        {"user":{"id":"user-1","username":"alice","email_address":null,
        "roles":["user"],"profile_image_id":null}}
        """.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [(identityData(), 200)]

        await authenticated.coordinator.finalize(authenticatedPending)

        XCTAssertEqual(authenticated.coordinator.result, .authenticated(provider: .linkedin))
        XCTAssertTrue(authenticated.sessionManager.isSignedIn)

        let connected = try makeFixture()
        let connectedPending = try claim(
            store: connected.store,
            flowId: "linkedin-connect",
            provider: .linkedin,
            purpose: .connect,
            now: connected.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/linkedin-connect/complete"
        ] = [(Data(#"{"oauth_account":{"id":"linkedin-1","name":"Alice","email_address":null}}"#.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [
            (identityData(provider: .linkedin), 200)
        ]

        await connected.coordinator.finalize(connectedPending)

        XCTAssertEqual(connected.coordinator.result, .connected(provider: .linkedin))
        XCTAssertTrue(connected.sessionManager.isSignedIn)
    }

    func testLinkedInFinalizationFailureCanRetryWithoutNewAuthorization() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "linkedin-retry",
            provider: .linkedin,
            purpose: .authenticate,
            now: fixture.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/linkedin-retry/complete"
        ] = [
            (Data(#"{"message":"offline"}"#.utf8), 503),
            (Data(#"{"mfa_required":true,"login_attempt_id":"linkedin-attempt"}"#.utf8), 200)
        ]

        await fixture.coordinator.finalize(pending)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .linkedin, loginAttemptId: "linkedin-attempt")
        )
        XCTAssertEqual(OAuthCoordinatorURLProtocol.requests.filter {
            $0.url?.path == "/api/v1/auth/oauth/authorizations/linkedin-retry/complete"
        }.count, 2)
    }
}
