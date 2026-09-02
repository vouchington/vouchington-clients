import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaModels
import XCTest

@MainActor
final class NativeOAuthAuthorizationCoordinatorTests: XCTestCase {
    override func setUp() {
        super.setUp()
        OAuthCoordinatorURLProtocol.responses = [:]
        OAuthCoordinatorURLProtocol.requests = []
    }

    func testLoadsCapabilitiesAndBeginsThenCancelsAuthorization() async throws {
        let fixture = try makeFixture()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = [
            (capabilitiesData(), 200)
        ]
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/github/authorizations"] = [
            (Data("""
            {
              "flow_id": "flow-1",
              "redirect_url": "https://github.com/login/oauth/authorize",
              "expires_at": "2027-01-01T00:05:00.000Z"
            }
            """.utf8), 200)
        ]
        var openedURL: URL?

        await fixture.coordinator.begin(provider: .github, purpose: .authenticate) {
            openedURL = $0
        }

        XCTAssertEqual(openedURL?.absoluteString, "https://github.com/login/oauth/authorize")
        XCTAssertEqual(fixture.coordinator.pending?.flowId, "flow-1")
        XCTAssertTrue(fixture.coordinator.canCancelPendingAuthorization)
        XCTAssertTrue(fixture.coordinator.supports(provider: .facebook, purpose: .connect))
        XCTAssertFalse(fixture.coordinator.supports(provider: .x, purpose: .authenticate))
        XCTAssertFalse(fixture.coordinator.isWorking)
        XCTAssertNil(fixture.coordinator.errorMessage)
        XCTAssertEqual(OAuthCoordinatorURLProtocol.requests.map(\.url?.path), [
            "/api/v1/auth/oauth/providers",
            "/api/v1/auth/oauth/github/authorizations"
        ])

        fixture.coordinator.cancelPendingAuthorization()
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.canCancelPendingAuthorization)
    }

    func testBeginRejectsUnsupportedInvalidAndFailedResponses() async throws {
        let fixture = try makeFixture()
        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/providers"] = [
            (capabilitiesData(), 200)
        ]
        await fixture.coordinator.begin(provider: .x, purpose: .authenticate) { _ in
            XCTFail("Unsupported provider should not open a URL")
        }
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertNil(fixture.coordinator.errorMessage)

        OAuthCoordinatorURLProtocol.responses["/api/v1/auth/oauth/github/authorizations"] = [
            (Data("""
            {
              "flow_id": "flow-invalid",
              "redirect_url": "voucha://auth/oauth/callback",
              "expires_at": "2027-01-01T00:05:00.000Z"
            }
            """.utf8), 200),
            (Data(#"{"message":"offline"}"#.utf8), 503)
        ]
        await fixture.coordinator.begin(provider: .github, purpose: .authenticate) { _ in
            XCTFail("An invalid URL should not open")
        }
        XCTAssertEqual(
            fixture.coordinator.errorMessage,
            NativeOAuthAuthorizationCoordinatorError.invalidAuthorization.localizedDescription
        )

        await fixture.coordinator.begin(provider: .github, purpose: .authenticate) { _ in
            XCTFail("A failed request should not open")
        }
        XCTAssertNotNil(fixture.coordinator.errorMessage)
    }

    func testHandleIncomingURLFinalizesMFAAndAcknowledgesResult() async throws {
        let fixture = try makeFixture()
        XCTAssertTrue(fixture.store.save(
            flowId: "flow-mfa",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: fixture.now.addingTimeInterval(300),
            now: fixture.now
        ))
        fixture.coordinator.synchronizeFromStore()
        XCTAssertFalse(try fixture.coordinator.handleIncomingURL(
            XCTUnwrap(URL(string: "https://example.com/callback"))
        ))
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-mfa/complete"
        ] = [(Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8), 200)]

        XCTAssertTrue(try fixture.coordinator.handleIncomingURL(XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=flow-mfa&completion_token=token"
        ))))
        await waitUntil { fixture.coordinator.result != nil }

        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .facebook, loginAttemptId: "attempt-1")
        )
        XCTAssertNil(fixture.coordinator.pending)
        fixture.coordinator.acknowledgeResult()
        XCTAssertNil(fixture.coordinator.result)
    }

    func testFinalizeAuthenticatedRefreshesSessionAndConnectedConfirmsIdentity() async throws {
        let authenticated = try makeFixture()
        let authenticatedPending = try claim(
            store: authenticated.store,
            flowId: "flow-auth",
            provider: .github,
            purpose: .authenticate,
            now: authenticated.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-auth/complete"
        ] = [(Data("""
        {
          "user": {
            "id": "user-1",
            "username": "alice",
            "email_address": null,
            "roles": ["user"],
            "profile_image_id": null
          }
        }
        """.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [
            (identityData(), 200)
        ]

        await authenticated.coordinator.finalize(authenticatedPending)

        XCTAssertEqual(authenticated.coordinator.result, .authenticated(provider: .github))
        XCTAssertTrue(authenticated.sessionManager.isSignedIn)

        let connected = try makeFixture()
        let connectedPending = try claim(
            store: connected.store,
            flowId: "flow-connect",
            provider: .x,
            purpose: .connect,
            now: connected.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-connect/complete"
        ] = [(Data(#"{"oauth_account":{"id":"x-1","name":"Alice","email_address":null}}"#.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [
            (identityData(provider: .x), 200)
        ]

        await connected.coordinator.finalize(connectedPending)

        XCTAssertEqual(connected.coordinator.result, .connected(provider: .x))
        XCTAssertTrue(connected.sessionManager.isSignedIn)
    }

    func testFinalizeReportsRefreshAndConnectionFailures() async throws {
        let refreshFailure = try makeFixture()
        let authPending = try claim(
            store: refreshFailure.store,
            flowId: "flow-refresh-failure",
            provider: .github,
            purpose: .authenticate,
            now: refreshFailure.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-refresh-failure/complete"
        ] = [(Data("""
        {
          "user": {
            "id": "user-1",
            "username": "alice",
            "email_address": null,
            "roles": [],
            "profile_image_id": null
          }
        }
        """.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [(Data("{}".utf8), 401)]

        await refreshFailure.coordinator.finalize(authPending)

        XCTAssertEqual(
            refreshFailure.coordinator.errorMessage,
            NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed.localizedDescription
        )
        XCTAssertNotNil(refreshFailure.coordinator.pending)

        let connectionFailure = try makeFixture()
        let connectPending = try claim(
            store: connectionFailure.store,
            flowId: "flow-connection-failure",
            provider: .facebook,
            purpose: .connect,
            now: connectionFailure.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-connection-failure/complete"
        ] = [(Data(#"{"oauth_account":{"id":"facebook-1","name":null,"email_address":null}}"#.utf8), 200)]
        OAuthCoordinatorURLProtocol.responses["/api/v1/my/identity"] = [(identityData(), 200)]

        await connectionFailure.coordinator.finalize(connectPending)

        XCTAssertEqual(
            connectionFailure.coordinator.errorMessage,
            NativeOAuthAuthorizationCoordinatorError.identityRefreshFailed.localizedDescription
        )
        XCTAssertNotNil(connectionFailure.coordinator.pending)
    }

    func testFailedFinalizationCanRetryOrCancelWithoutRestarting() async throws {
        let retry = try makeFixture()
        let retryPending = try claim(
            store: retry.store,
            flowId: "flow-retry",
            provider: .github,
            purpose: .authenticate,
            now: retry.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-retry/complete"
        ] = [
            (Data(#"{"message":"offline"}"#.utf8), 503),
            (Data(#"{"mfa_required":true,"login_attempt_id":"attempt-retry"}"#.utf8), 200)
        ]

        await retry.coordinator.finalize(retryPending)

        XCTAssertTrue(retry.coordinator.canRecoverFailedFinalization)
        XCTAssertFalse(retry.coordinator.canCancelPendingAuthorization)
        await retry.coordinator.retryFailedFinalization()
        XCTAssertEqual(
            retry.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-retry")
        )
        XCTAssertFalse(retry.coordinator.canRecoverFailedFinalization)

        let cancellation = try makeFixture()
        let cancellationPending = try claim(
            store: cancellation.store,
            flowId: "flow-cancel",
            provider: .facebook,
            purpose: .connect,
            now: cancellation.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/flow-cancel/complete"
        ] = [(Data(#"{"message":"offline"}"#.utf8), 503)]
        await cancellation.coordinator.finalize(cancellationPending)

        XCTAssertTrue(cancellation.coordinator.canRecoverFailedFinalization)
        cancellation.coordinator.cancelPendingAuthorization()
        XCTAssertNil(cancellation.coordinator.pending)
        XCTAssertFalse(cancellation.coordinator.canRecoverFailedFinalization)
    }

    func testSynchronizationRecordsExpiredAndResumeFinalizesClaimedAuthorization() async throws {
        let expired = try makeFixture()
        XCTAssertTrue(expired.store.save(
            flowId: "expired",
            provider: .facebook,
            purpose: .connect,
            completionProofVerifier: "verifier",
            expiresAt: expired.now.addingTimeInterval(-1),
            now: expired.now.addingTimeInterval(-2)
        ))
        expired.coordinator.synchronizeFromStore()
        XCTAssertEqual(expired.coordinator.result, .expired(provider: .facebook, purpose: .connect))
        XCTAssertEqual(expired.store.pendingStatus(now: expired.now), .none)

        let active = try makeFixture()
        let pending = try claim(
            store: active.store,
            flowId: "resume",
            provider: .github,
            purpose: .authenticate,
            now: active.now
        )
        OAuthCoordinatorURLProtocol.responses[
            "/api/v1/auth/oauth/authorizations/resume/complete"
        ] = [(Data(#"{"mfa_required":true,"login_attempt_id":"attempt-resume"}"#.utf8), 200)]
        active.coordinator.synchronizeFromStore()
        XCTAssertEqual(active.coordinator.pending, pending)

        await active.coordinator.resumePendingAuthorization()

        XCTAssertEqual(
            active.coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "attempt-resume")
        )
        await active.coordinator.resumePendingAuthorization()
    }

    func testStoreLeaseClearRecordAndProofModelBranches() throws {
        let fixture = try makeFixture()
        let pending = try claim(
            store: fixture.store,
            flowId: "lease",
            provider: .github,
            purpose: .connect,
            now: fixture.now
        )
        XCTAssertTrue(try fixture.store.acquireFinalizationLease(for: pending, now: fixture.now))
        XCTAssertFalse(try fixture.store.acquireFinalizationLease(for: pending, now: fixture.now))
        fixture.store.releaseFinalizationLease(flowId: pending.flowId)
        XCTAssertTrue(try fixture.store.acquireFinalizationLease(for: pending, now: fixture.now))
        fixture.store.releaseFinalizationLease(flowId: pending.flowId)

        try fixture.store.record(.connected(provider: .github))
        try fixture.store.clearPending()
        XCTAssertEqual(fixture.store.result(), .connected(provider: .github))
        try fixture.store.acknowledgeResult()
        try fixture.store.acknowledgeResult()

        let generated = try NativeAuthorizationCompletionProof.generate()
        XCTAssertEqual(generated.verifier.count, 43)
        XCTAssertEqual(generated.challenge.count, 43)
        XCTAssertEqual(
            NativeAuthorizationCompletionProof.fromVerifier("verifier").challenge,
            "iMnq5o6zALKXGivsnlom_0F5_WYda32GHkxlV7mq7hQ"
        )
        XCTAssertEqual(NativeOAuthProvider.allCases.map(\.id), ["facebook", "x", "github"])

        let capability = OAuthBrokerCapability(
            version: 2,
            modes: OAuthBrokerModes(web: true, native: true),
            purposes: [.authenticate]
        )
        XCTAssertFalse(capability.supportsNative(.authenticate))
    }

    private func makeFixture() throws -> CoordinatorFixture {
        let now = try XCTUnwrap(
            ISO8601DateFormatter().date(from: "2027-01-01T00:00:00Z")
        )
        let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [OAuthCoordinatorURLProtocol.self],
            bootstrapSession: false
        )
        let store = NativeOAuthAuthorizationStore(defaults: defaults)
        let sessionManager = SessionManager(client: client, cookieStorage: IsolatedHTTPCookieStorage.make())
        return CoordinatorFixture(
            coordinator: NativeOAuthAuthorizationCoordinator(
                client: client,
                sessionManager: sessionManager,
                store: store,
                now: { now }
            ),
            sessionManager: sessionManager,
            store: store,
            now: now
        )
    }

    private func claim(
        store: NativeOAuthAuthorizationStore,
        flowId: String,
        provider: NativeOAuthProvider,
        purpose: NativeOAuthAuthorizationPurpose,
        now: Date
    ) throws -> PendingNativeOAuthAuthorization {
        XCTAssertTrue(store.save(
            flowId: flowId,
            provider: provider,
            purpose: purpose,
            completionProofVerifier: "verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        return try XCTUnwrap(try store.claimCallback(for: XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=\(flowId)&completion_token=token"
        )), now: now))
    }

    private func capabilitiesData() -> Data {
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

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 500 where !condition() {
            await Task.yield()
        }
    }
}

private struct CoordinatorFixture {
    let coordinator: NativeOAuthAuthorizationCoordinator
    let sessionManager: SessionManager
    let store: NativeOAuthAuthorizationStore
    let now: Date
}

private final class OAuthCoordinatorURLProtocol: URLProtocol {
    static let lock = NSLock()
    static var responses: [String: [(Data, Int)]] = [:]
    static var requests: [URLRequest] = []

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        let response = Self.lock.withLock { () -> (Data, Int) in
            Self.requests.append(request)
            guard var queued = Self.responses[path], !queued.isEmpty else {
                return (Data("{}".utf8), 200)
            }
            let next = queued.removeFirst()
            Self.responses[path] = queued
            return next
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
