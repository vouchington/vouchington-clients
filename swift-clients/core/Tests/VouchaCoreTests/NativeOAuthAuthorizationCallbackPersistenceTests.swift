import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

@MainActor
final class NativeOAuthAuthorizationCallbackPersistenceTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CallbackPersistenceURLProtocol.responses = [:]
    }

    func testCallbackPersistenceFailureRetainsTokenForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        fixture.persistence.failNextCallbackWrite = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)

        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(callbackURL()))

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending?.completionToken, nil)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=other-flow&completion_token=other-token"
        ))))
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testCallbackReadFailurePreservesPendingForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        fixture.persistence.failNextCallbackRead = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)

        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(callbackURL()))

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending?.flowId, "callback-flow")
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testMFAResultPersistenceFailurePreservesFinalizationForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        let claimed = try XCTUnwrap(try fixture.store.claimCallback(
            for: callbackURL(),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.persistence.failNextWrite = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)

        await fixture.coordinator.finalize(claimed)

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending, claimed)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testFinalizationLeaseReadFailurePreservesFinalizationForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        let claimed = try XCTUnwrap(try fixture.store.claimCallback(
            for: callbackURL(),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.persistence.failNextCallbackRead = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)

        await fixture.coordinator.finalize(claimed)

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending, claimed)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testAuthenticatedResultPersistenceFailurePreservesFinalizationForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        let claimed = try XCTUnwrap(try fixture.store.claimCallback(
            for: callbackURL(),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.persistence.failNextWrite = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (
            Data(
                #"{"user":{"id":"user-1","username":"alice","email_address":null,"roles":["user"],"profile_image_id":null}}"#
                    .utf8
            ),
            200
        )
        CallbackPersistenceURLProtocol.responses["/api/v1/my/identity"] = (identityData(), 200)

        await fixture.coordinator.finalize(claimed)

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending, claimed)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(fixture.coordinator.result, .authenticated(provider: .github))
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testConnectedResultPersistenceFailurePreservesFinalizationForRetry() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture, purpose: .connect)
        let claimed = try XCTUnwrap(try fixture.store.claimCallback(
            for: callbackURL(),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.persistence.failNextWrite = true
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"oauth_account":{"id":"github-1","name":"Alice","email_address":null}}"#.utf8), 200)
        CallbackPersistenceURLProtocol.responses["/api/v1/my/identity"] = (
            identityData(provider: .github),
            200
        )

        await fixture.coordinator.finalize(claimed)

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertEqual(fixture.coordinator.pending, claimed)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(fixture.coordinator.result, .connected(provider: .github))
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testCancelDropsRetainedCallbackBeforeFreshAuthorization() async throws {
        let fixture = try makeFixture()
        try seedPending(fixture)
        fixture.persistence.failNextCallbackWrite = true
        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(callbackURL()))
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        fixture.coordinator.cancelPendingAuthorization()

        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
        CallbackPersistenceURLProtocol.responses["/api/v1/auth/oauth/providers"] = (
            capabilitiesData(),
            200
        )
        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/github/authorizations"
        ] = (Data("""
        {
          "flow_id": "fresh-flow",
          "redirect_url": "https://github.com/login/oauth/authorize",
          "expires_at": "2027-01-01T00:05:00.000Z"
        }
        """.utf8), 200)
        var openedURL: URL?

        await fixture.coordinator.begin(provider: .github, purpose: .authenticate) {
            openedURL = $0
        }

        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertEqual(fixture.coordinator.pending?.flowId, "fresh-flow")
        XCTAssertEqual(openedURL?.host, "github.com")
    }

    func testInitializationReadFailureRetainsFinalizingVerifierForResume() async throws {
        let persistence = FlakyOAuthSecureState()
        let fixture = try makeFixture(
            persistence: persistence,
            readsUnavailableOnInitialization: true
        ) { store, now in
            XCTAssertTrue(store.save(
                flowId: "callback-flow",
                provider: .github,
                purpose: .authenticate,
                completionProofVerifier: "verifier",
                expiresAt: now.addingTimeInterval(300),
                now: now
            ))
            _ = try XCTUnwrap(try store.claimCallback(for: self.callbackURL(), now: now))
        }

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertEqual(persistence.deleteCount, 0)

        CallbackPersistenceURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/callback-flow/complete"
        ] = (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)
        persistence.readsUnavailable = false
        await fixture.coordinator.resumePendingAuthorization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertEqual(persistence.deleteCount, 0)
    }

    func testColdCallbackReadFailureExposesGenericRecoveryState() throws {
        let persistence = FlakyOAuthSecureState()
        let fixture = try makeFixture(
            persistence: persistence,
            readsUnavailableOnInitialization: true
        ) { store, now in
            XCTAssertTrue(store.save(
                flowId: "callback-flow",
                provider: .github,
                purpose: .connect,
                completionProofVerifier: "verifier",
                expiresAt: now.addingTimeInterval(300),
                now: now
            ))
        }

        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(callbackURL()))

        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
    }

    func testInitializationReadFailureRetainsMFAResultForResume() async throws {
        let persistence = FlakyOAuthSecureState()
        let fixture = try makeFixture(
            persistence: persistence,
            readsUnavailableOnInitialization: true
        ) { store, _ in
            try store.record(.mfaRequired(provider: .github, loginAttemptId: "attempt-1"))
        }

        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        XCTAssertEqual(persistence.deleteCount, 0)

        persistence.readsUnavailable = false
        await fixture.coordinator.retryFailedFinalization()

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertFalse(fixture.coordinator.canRecoverFailedFinalization)
        XCTAssertEqual(persistence.deleteCount, 0)
    }

    func testMalformedSecureStateIsDeletedDuringInitialization() throws {
        let persistence = FlakyOAuthSecureState()
        persistence.seed(Data("malformed".utf8))

        let fixture = try makeFixture(persistence: persistence)

        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.result)
        XCTAssertEqual(persistence.deleteCount, 1)
    }

    private func makeFixture(
        persistence: FlakyOAuthSecureState = FlakyOAuthSecureState(),
        readsUnavailableOnInitialization: Bool = false,
        prepareStore: (NativeOAuthAuthorizationStore, Date) throws -> Void = { _, _ in }
    ) throws -> CallbackPersistenceFixture {
        let now = try XCTUnwrap(ISO8601DateFormatter().date(from: "2027-01-01T00:00:00Z"))
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [CallbackPersistenceURLProtocol.self],
            bootstrapSession: false
        )
        let store = NativeOAuthAuthorizationStore(secureState: persistence)
        try prepareStore(store, now)
        persistence.readsUnavailable = readsUnavailableOnInitialization
        return CallbackPersistenceFixture(
            coordinator: NativeOAuthAuthorizationCoordinator(
                client: client,
                sessionManager: SessionManager(
                    client: client,
                    cookieStorage: IsolatedHTTPCookieStorage.make()
                ),
                store: store,
                now: { now }
            ),
            persistence: persistence,
            store: store,
            now: now
        )
    }

    private func seedPending(
        _ fixture: CallbackPersistenceFixture,
        purpose: NativeOAuthAuthorizationPurpose = .authenticate
    ) throws {
        XCTAssertTrue(fixture.store.save(
            flowId: "callback-flow",
            provider: .github,
            purpose: purpose,
            completionProofVerifier: "verifier",
            expiresAt: fixture.now.addingTimeInterval(300),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
    }

    private func callbackURL() throws -> URL {
        try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=callback-flow&completion_token=retained-token"
        ))
    }

    private func capabilitiesData() -> Data {
        Data("""
        {
          "providers": ["facebook", "x", "github"],
          "broker_capabilities": {
            "facebook": {
              "version": 1,
              "modes": {"web": true,"native": true},
              "purposes": ["authenticate", "connect"]
            },
            "x": {
              "version": 1,
              "modes": {"web": true,"native": false},
              "purposes": ["authenticate", "connect"]
            },
            "github": {
              "version": 1,
              "modes": {"web": true,"native": true},
              "purposes": ["authenticate", "connect"]
            }
          }
        }
        """.utf8)
    }

    private func identityData(provider: NativeOAuthProvider? = nil) -> Data {
        var identity = try! JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("swift.my.identity.default")
        ) as! [String: Any]
        if let provider {
            var body = identity["identity"] as! [String: Any]
            body["\(provider.rawValue)_account"] = [
                "id": "\(provider.rawValue)-1",
                "name": "Alice",
                "email_address": NSNull()
            ]
            identity["identity"] = body
        }
        return try! JSONSerialization.data(withJSONObject: identity)
    }
}

private struct CallbackPersistenceFixture {
    let coordinator: NativeOAuthAuthorizationCoordinator
    let persistence: FlakyOAuthSecureState
    let store: NativeOAuthAuthorizationStore
    let now: Date
}

private final class CallbackPersistenceURLProtocol: URLProtocol {
    static let lock = NSLock()
    static var responses: [String: (Data, Int)] = [:]

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let response = Self.lock.withLock {
            Self.responses[request.url?.path ?? ""] ?? (Data("{}".utf8), 200)
        }
        let http = HTTPURLResponse(
            url: request.url!,
            statusCode: response.1,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: http, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: response.0)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
