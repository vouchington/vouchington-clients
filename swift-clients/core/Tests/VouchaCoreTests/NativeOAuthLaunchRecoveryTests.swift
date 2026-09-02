import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

@MainActor
final class NativeOAuthLaunchRecoveryTests: XCTestCase {
    override func setUp() {
        super.setUp()
        LaunchRecoveryURLProtocol.reset()
    }

    func testColdLaunchResumesClaimedCallbackExactlyOnce() async throws {
        let now = try XCTUnwrap(ISO8601DateFormatter().date(from: "2027-01-01T00:00:00Z"))
        let persistence = FlakyOAuthSecureState()
        let storeBeforeTermination = NativeOAuthAuthorizationStore(secureState: persistence)
        XCTAssertTrue(storeBeforeTermination.save(
            flowId: "cold-launch-flow",
            provider: .github,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        let callbackURL = try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=cold-launch-flow&completion_token=token"
        ))
        _ = try XCTUnwrap(try storeBeforeTermination.claimCallback(for: callbackURL, now: now))
        LaunchRecoveryURLProtocol.response = (
            Data(#"{"mfa_required":true,"login_attempt_id":"cold-launch-attempt"}"#.utf8),
            200
        )

        let coordinator = try makeCoordinator(
            store: NativeOAuthAuthorizationStore(secureState: persistence),
            now: now
        )

        await coordinator.resumePendingAuthorization()
        await coordinator.resumePendingAuthorization()

        XCTAssertEqual(
            coordinator.result,
            .mfaRequired(provider: .github, loginAttemptId: "cold-launch-attempt")
        )
        XCTAssertNil(coordinator.pending)
        XCTAssertEqual(LaunchRecoveryURLProtocol.requestCount, 1)
    }

    private func makeCoordinator(
        store: NativeOAuthAuthorizationStore,
        now: Date
    ) throws -> NativeOAuthAuthorizationCoordinator {
        let cookies = IsolatedHTTPCookieStorage.make()
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: cookies,
            protocolClasses: [LaunchRecoveryURLProtocol.self],
            bootstrapSession: false
        )
        return NativeOAuthAuthorizationCoordinator(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: cookies),
            store: store,
            now: { now }
        )
    }
}

private final class LaunchRecoveryURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var state = State()

    private struct State {
        var response = (Data("{}".utf8), 200)
        var requestCount = 0
    }

    static var response: (Data, Int) {
        get { lock.withLock { state.response } }
        set { lock.withLock { state.response = newValue } }
    }

    static var requestCount: Int {
        lock.withLock { state.requestCount }
    }

    static func reset() {
        lock.withLock { state = State() }
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let response = Self.lock.withLock {
            Self.state.requestCount += 1
            return Self.state.response
        }
        guard let url = request.url,
              let httpResponse = HTTPURLResponse(
                  url: url,
                  statusCode: response.1,
                  httpVersion: "HTTP/1.1",
                  headerFields: ["Content-Type": "application/json"]
              )
        else { return }
        client?.urlProtocol(self, didReceive: httpResponse, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: response.0)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        // Responses are delivered synchronously.
    }
}
