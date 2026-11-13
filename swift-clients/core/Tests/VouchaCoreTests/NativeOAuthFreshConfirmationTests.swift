import Foundation
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

extension NativeOAuthAuthorizationCoordinatorTests {
    func testFreshBrokerConfirmationDoesNotRefreshSessionOrCreateMFAAssurance() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "reauth-flow",
            provider: .github,
            purpose: .reauthenticate,
            now: fixture.now
        )
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/authorizations/reauth-flow/complete"] = [
            (Data(#"{"oauth_account":{"id":"linked-account","name":"Alice","email_address":null}}"#.utf8), 200)
        ]
        await fixture.coordinator.finalize(pending)
        XCTAssertEqual(fixture.coordinator.result, .confirmationReady(provider: .github, flowId: "reauth-flow"))
        XCTAssertNil(fixture.coordinator.result?.mfaLoginAttemptId)
        XCTAssertFalse(OAuthCoordinatorURLProtocol.requests.contains { $0.url?.path == "/api/v1/my/identity" })
        XCTAssertFalse(OAuthCoordinatorURLProtocol.requests.contains { $0.url?.path == "/api/v1/session" })
    }

    func testFreshBrokerRejectsAuthenticationAndMFAPurposeConfusionWithoutRefreshing() async throws {
        let wrongResponses = [
            #"{"user":{"id":"another-user","username":null,"email_address":null,"roles":[],"profile_image_id":null}}"#,
            #"{"mfa_required":true,"login_attempt_id":"attempt"}"#
        ]
        for body in wrongResponses {
            let fixture = try makeFixture()
            let pending = try claim(
                store: fixture.store,
                flowId: "wrong-purpose",
                provider: .github,
                purpose: .reauthenticate,
                now: fixture.now
            )
            OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/authorizations/wrong-purpose/complete"] = [
                (Data(body.utf8), 200)
            ]
            await fixture.coordinator.finalize(pending)
            XCTAssertNotNil(fixture.coordinator.errorMessage)
            XCTAssertNil(fixture.coordinator.result)
            XCTAssertFalse(OAuthCoordinatorURLProtocol.requests.contains { $0.url?.path == "/api/v1/my/identity" })
            fixture.coordinator.cancelFreshAccountConfirmation()
        }
    }

    func testCanceledFreshBrokerCannotPublishALateConfirmation() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "canceled-flow",
            provider: .github,
            purpose: .reauthenticate,
            now: fixture.now
        )
        await fixture.coordinator.resumePendingAuthorization()
        fixture.coordinator.cancelFreshAccountConfirmation()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/authorizations/canceled-flow/complete"] = [
            (Data(#"{"oauth_account":{"id":"linked-account","name":null,"email_address":null}}"#.utf8), 200)
        ]
        await fixture.coordinator.finalize(pending)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(fixture.coordinator.pending)
    }
}
