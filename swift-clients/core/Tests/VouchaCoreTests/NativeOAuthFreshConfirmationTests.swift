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

    func testCancelingFreshBrokerStopsPollingAndIgnoresALateCompletion() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "polling-flow",
            provider: .github,
            purpose: .reauthenticate,
            now: fixture.now
        )
        let path = "/api/v1/auth/oauth/authorizations/polling-flow/complete"
        func pollCount() -> Int {
            OAuthCoordinatorURLProtocol.lock.withLock {
                OAuthCoordinatorURLProtocol.requests.filter { $0.url?.path == path }.count
            }
        }
        OAuthCoordinatorURLProtocol.lock.withLock {
            OAuthCoordinatorURLProtocol.responses[path] = Array(
                repeating: (Data(#"{"status":"pending"}"#.utf8), 200),
                count: 20
            )
        }
        fixture.coordinator.synchronizeFromStore()
        let task = Task { await fixture.coordinator.finalize(pending) }
        for _ in 0 ..< 500 where pollCount() < 1 {
            try await Task.sleep(for: .milliseconds(10))
        }
        XCTAssertGreaterThanOrEqual(pollCount(), 1)

        fixture.coordinator.cancelFreshAccountConfirmation()
        let pollsAtCancel = pollCount()
        OAuthCoordinatorURLProtocol.lock.withLock {
            OAuthCoordinatorURLProtocol.responses[path] = [
                (Data(#"{"oauth_account":{"id":"linked-account","name":null,"email_address":null}}"#.utf8), 200)
            ]
        }
        await task.value

        XCTAssertEqual(pollCount(), pollsAtCancel)
        XCTAssertFalse(fixture.coordinator.isWorking)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertTrue(fixture.coordinator.canStartAuthorization)
    }

    func testRestoredConfirmationWithoutLiveOwnerIsDiscardedButOwnedOneIsKept() async throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "orphan-flow",
            provider: .github,
            purpose: .reauthenticate,
            now: fixture.now
        )
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/authorizations/orphan-flow/complete"] = [
            (Data(#"{"oauth_account":{"id":"linked-account","name":null,"email_address":null}}"#.utf8), 200)
        ]
        await fixture.coordinator.finalize(pending)
        XCTAssertEqual(fixture.coordinator.result, .confirmationReady(provider: .github, flowId: "orphan-flow"))

        fixture.coordinator.hasFreshConfirmationOwner = true
        fixture.coordinator.discardFreshConfirmationWithoutOwner()
        XCTAssertNotNil(fixture.coordinator.result)

        fixture.coordinator.hasFreshConfirmationOwner = false
        fixture.coordinator.discardFreshConfirmationWithoutOwner()
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canStartAuthorization)
        fixture.coordinator.discardFreshConfirmationWithoutOwner()
        XCTAssertNil(fixture.coordinator.result)
    }

    func testRestoredConfirmationExpiryWithoutLiveOwnerIsDiscarded() throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "expired-orphan",
            provider: .github,
            purpose: .reauthenticate,
            now: fixture.now
        )
        XCTAssertEqual(pending.purpose, .reauthenticate)
        try fixture.store.record(.expired(provider: .github, purpose: .reauthenticate))
        fixture.coordinator.synchronizeFromStore()
        XCTAssertEqual(fixture.coordinator.result, .expired(provider: .github, purpose: .reauthenticate))
        XCTAssertFalse(fixture.coordinator.canStartAuthorization)

        fixture.coordinator.discardFreshConfirmationWithoutOwner()
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canStartAuthorization)
    }
}
