import ViewInspector
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class SignInViewTests: NativeRouteSurfaceViewModelTestCase {
    func testSignInViewRendersLegalLinksAndRoutesThemThroughTheNativeTargetHandler() throws {
        var navigatedPaths: [String] = []

        let sut = SignInView(
            signInService: makeService(),
            onNavigateToTargetPath: { navigatedPaths.append($0) },
            onSuccess: {}
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Privacy Policy"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Terms of Service"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Community Guidelines"))

        try sut.inspect().find(button: "Privacy Policy").tap()
        try sut.inspect().find(button: "Terms of Service").tap()
        try sut.inspect().find(button: "Community Guidelines").tap()

        XCTAssertEqual(
            navigatedPaths,
            [
                "/article/privacy-policy",
                "/article/terms-of-service",
                "/article/community-guidelines"
            ]
        )
    }

    func testLegalTargetNavigationUsesTheNativeTargetHandler() {
        var navigatedPaths: [String] = []
        let sut = SignInView(
            signInService: makeService(),
            onNavigateToTargetPath: { navigatedPaths.append($0) },
            onSuccess: {}
        )

        sut.navigateToLegalTargetPath("/article/privacy-policy")

        XCTAssertEqual(navigatedPaths, ["/article/privacy-policy"])
    }

    func testNativeOAuthCapabilitiesRenderOnlySupportedProviderButtons() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            oauthCapabilities(),
            200
        )
        let fixture = try makeOAuthFixture()
        await fixture.coordinator.loadCapabilities()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Facebook"))
        XCTAssertNoThrow(try sut.inspect().find(button: "GitHub"))
        XCTAssertThrowsError(try sut.inspect().find(button: "X"))

        fixture.coordinator.isWorking = true
        XCTAssertTrue(try sut.inspect().find(button: "Facebook").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "GitHub").isDisabled())

        fixture.coordinator.isWorking = false
        fixture.coordinator.result = .connected(provider: .github)
        XCTAssertTrue(try sut.inspect().find(button: "Facebook").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "GitHub").isDisabled())
    }

    func testNativeOAuthResultRoutesAuthenticationMFAAndNoOpResults() throws {
        let fixture = try makeOAuthFixture()
        var successCount = 0
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: { successCount += 1 }
        )

        fixture.coordinator.result = .authenticated(provider: .github)
        sut.consumeNativeOAuthResult(fixture.coordinator.result)
        XCTAssertEqual(successCount, 1)
        XCTAssertNil(fixture.coordinator.result)

        sut.consumeNativeOAuthResult(.mfaRequired(
            provider: .facebook,
            loginAttemptId: "attempt-1"
        ))

        sut.consumeNativeOAuthResult(.connected(provider: .github))
        sut.consumeNativeOAuthResult(.expired(provider: .x, purpose: .authenticate))
        sut.consumeNativeOAuthResult(nil)
        XCTAssertEqual(successCount, 1)
    }

    func testExpiredNativeOAuthResultRemainsUntilFailureIsDismissed() throws {
        let fixture = try makeOAuthFixture()
        try fixture.coordinator.store.record(.expired(
            provider: .github,
            purpose: .authenticate
        ))
        fixture.coordinator.synchronizeFromStore()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        sut.consumeNativeOAuthResult(fixture.coordinator.result)

        XCTAssertEqual(
            fixture.coordinator.result,
            .expired(provider: .github, purpose: .authenticate)
        )
        XCTAssertEqual(
            sut.oauthPresentation.error,
            .message(.nativeSwiftSettingsOauthExpired)
        )

        sut.dismissNativeOAuthError()

        XCTAssertNil(fixture.coordinator.result)
        XCTAssertNil(sut.oauthPresentation.error)
    }

    func testCapabilityLoadFailureCanRetryWithoutRecreatingSignIn() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )
        let fixture = try makeOAuthFixture()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        await fixture.coordinator.loadCapabilities()
        sut.synchronizeNativeOAuthError()

        XCTAssertNotNil(sut.oauthPresentation.error)
        XCTAssertTrue(fixture.coordinator.canRetryCapabilityLoading)

        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            oauthCapabilities(),
            200
        )
        await sut.retryNativeOAuthCapabilityLoading()

        XCTAssertNil(sut.oauthPresentation.error)
        XCTAssertFalse(fixture.coordinator.canRetryCapabilityLoading)
        XCTAssertTrue(fixture.coordinator.supports(provider: .github, purpose: .authenticate))
    }

    func testCancellingNativeOAuthMFAAcknowledgesPersistedResult() throws {
        let fixture = try makeOAuthFixture()
        try fixture.coordinator.store.record(.mfaRequired(
            provider: .facebook,
            loginAttemptId: "attempt-1"
        ))
        fixture.coordinator.synchronizeFromStore()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            initialOAuthMFAAttemptId: "attempt-1",
            onSuccess: {}
        )
        sut.cancelNativeOAuthMFAChallenge()
        XCTAssertNil(fixture.coordinator.result)
        let restarted = NativeOAuthAuthorizationCoordinator(
            client: fixture.coordinator.client,
            sessionManager: fixture.coordinator.sessionManager,
            store: fixture.coordinator.store
        )
        XCTAssertNil(restarted.result)
    }

    func testInteractiveMFADismissalAcknowledgesOnlyNativeOAuthResult() throws {
        let fixture = try makeOAuthFixture()
        try fixture.coordinator.store.record(.mfaRequired(
            provider: .facebook,
            loginAttemptId: "attempt-1"
        ))
        fixture.coordinator.synchronizeFromStore()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            initialOAuthMFAAttemptId: "attempt-1",
            onSuccess: {}
        )

        sut.nativeOAuthMFASheetIsPresented.wrappedValue = false

        XCTAssertNil(fixture.coordinator.result)
        try fixture.coordinator.store.record(.connected(provider: .github))
        fixture.coordinator.synchronizeFromStore()
        let nonOAuthSUT = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )
        nonOAuthSUT.cancelNativeOAuthMFAChallenge()
        XCTAssertEqual(fixture.coordinator.result, .connected(provider: .github))
    }

    func testNativeOAuthBeginFailureShowsErrorAndMissingCoordinatorIsSafe() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/providers"] = (
            oauthCapabilities(),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/oauth/facebook/authorizations"] = (
            Data(#"{"message":"offline"}"#.utf8),
            503
        )
        let fixture = try makeOAuthFixture()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        await sut.beginNativeOAuthSignIn(.facebook)

        XCTAssertNotNil(fixture.coordinator.errorMessage)

        let detached = SignInView(signInService: fixture.service, onSuccess: {})
        await detached.beginNativeOAuthSignIn(.facebook)
        XCTAssertNil(detached.oauthPresentation.error)
    }

    func testNativeOAuthBeginPreservesUnconsumedResult() async throws {
        let fixture = try makeOAuthFixture()
        try fixture.coordinator.store.record(.mfaRequired(
            provider: .facebook,
            loginAttemptId: "existing-attempt"
        ))
        fixture.coordinator.synchronizeFromStore()
        var openedURL: URL?

        await fixture.coordinator.begin(provider: .github, purpose: .authenticate) {
            openedURL = $0
        }

        XCTAssertNil(openedURL)
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertEqual(
            fixture.coordinator.result,
            .mfaRequired(provider: .facebook, loginAttemptId: "existing-attempt")
        )
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/auth/oauth/providers"), 0)
    }

    func testNativeOAuthBeginRevalidatesAfterCapabilityLoading() async throws {
        let capabilitiesPath = "/api/v1/auth/oauth/providers"
        CannedFeedURLProtocol.handlers[capabilitiesPath] = (oauthCapabilities(), 200)
        CannedFeedURLProtocol.suspendResponse(path: capabilitiesPath)
        let requestBarrier = CannedFeedURLProtocol.requestBarrier(path: capabilitiesPath, method: "GET")
        let fixture = try makeOAuthFixture()
        var openedURL: URL?
        let begin = Task {
            await fixture.coordinator.begin(provider: .github, purpose: .authenticate) {
                openedURL = $0
            }
        }

        _ = try await requestBarrier.wait()
        try fixture.coordinator.store.record(.connected(provider: .github))
        fixture.coordinator.synchronizeFromStore()
        CannedFeedURLProtocol.releaseResponse(path: capabilitiesPath)
        await begin.value

        XCTAssertNil(openedURL)
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertEqual(fixture.coordinator.result, .connected(provider: .github))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedPathCount("/api/v1/auth/oauth/github/authorizations"),
            0
        )
    }

    func testNativeOAuthFinalizationFailureCanBeCancelledFromSignIn() throws {
        let fixture = try makeOAuthFixture()
        let now = Date()
        XCTAssertTrue(fixture.coordinator.store.save(
            flowId: "sign-in-recovery",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: now.addingTimeInterval(300),
            now: now
        ))
        let callback = try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=sign-in-recovery&completion_token=token"
        ))
        XCTAssertNotNil(try fixture.coordinator.store.claimCallback(for: callback, now: now))
        fixture.coordinator.synchronizeFromStore()
        fixture.coordinator.errorMessage = "Connection failed"
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        sut.synchronizeNativeOAuthError()
        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)

        sut.cancelNativeOAuthAuthorization()
        XCTAssertNil(fixture.coordinator.pending)
    }

    func testColdCallbackWithoutPurposeShowsGenericRecoveryAlert() throws {
        let fixture = try makeOAuthFixture()
        fixture.coordinator.retainedCallbackURL = try XCTUnwrap(URL(
            string: "voucha://auth/oauth/callback?flow_id=cold-flow&completion_token=token"
        ))
        fixture.coordinator.errorMessage = NativeOAuthSecureStateError.unavailable.localizedDescription
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        sut.synchronizeNativeOAuthError()
        let alert = try sut.inspect().navigationStack().vStack().alert()

        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        XCTAssertEqual(try alert.title().string(), "Sign In Failed")
        XCTAssertEqual(
            try alert.message().text().string(),
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
    }

    func testInitialSecureStateReadFailureShowsSynchronizationRetry() throws {
        let fixture = try makeOAuthFixture()
        fixture.coordinator.secureStateReadUnavailable = true
        fixture.coordinator.errorMessage = NativeOAuthSecureStateError.unavailable.localizedDescription
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        sut.synchronizeNativeOAuthError()
        let alert = try sut.inspect().navigationStack().vStack().alert()

        XCTAssertTrue(fixture.coordinator.canRecoverFailedFinalization)
        XCTAssertNoThrow(try alert.find(button: "Try Again"))
        XCTAssertEqual(
            try alert.message().text().string(),
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
    }

    func testWaitingNativeOAuthCanBeCancelledFromSignIn() throws {
        let fixture = try makeOAuthFixture()
        XCTAssertTrue(fixture.coordinator.store.save(
            flowId: "sign-in-waiting",
            provider: .github,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: Date().addingTimeInterval(300)
        ))
        fixture.coordinator.synchronizeFromStore()
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: {}
        )

        try sut.authButtonsSection.inspect().find(button: "Cancel").tap()

        XCTAssertNil(fixture.coordinator.pending)
    }

    func testSuccessfulNonBrokerSignInDiscardsPersistedOAuthAuthorization() throws {
        let fixture = try makeOAuthFixture()
        XCTAssertTrue(fixture.coordinator.store.save(
            flowId: "superseded-oauth",
            provider: .github,
            purpose: .authenticate,
            completionProofVerifier: "verifier",
            expiresAt: Date().addingTimeInterval(300)
        ))
        let finalizing = try XCTUnwrap(try fixture.coordinator.store.claimCallback(
            for: XCTUnwrap(URL(
                string: "voucha://auth/oauth/callback?flow_id=superseded-oauth&completion_token=token"
            ))
        ))
        fixture.coordinator.synchronizeFromStore()
        fixture.coordinator.isWorking = true
        var successCount = 0
        let sut = SignInView(
            signInService: fixture.service,
            nativeOAuthAuthorizationCoordinator: fixture.coordinator,
            onSuccess: { successCount += 1 }
        )

        sut.completeSuccessfulSignIn()

        XCTAssertEqual(successCount, 1)
        XCTAssertNil(fixture.coordinator.pending)
        XCTAssertFalse(fixture.coordinator.store.complete(
            finalizing,
            with: .authenticated(provider: .github)
        ))
        XCTAssertNil(fixture.coordinator.store.result())
        let restarted = NativeOAuthAuthorizationCoordinator(
            client: fixture.coordinator.client,
            sessionManager: fixture.coordinator.sessionManager,
            store: fixture.coordinator.store
        )
        XCTAssertNil(restarted.pending)
        XCTAssertTrue(fixture.coordinator.store.save(
            flowId: "new-oauth",
            provider: .facebook,
            purpose: .authenticate,
            completionProofVerifier: "new-verifier",
            expiresAt: Date().addingTimeInterval(300)
        ))
    }

    private func makeService() -> SignInService {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let storage = HTTPCookieStorage()
        let client = APIClient(
            config: config,
            cookieStorage: storage,
            protocolClasses: [EmailOTPViewFailingURLProtocol.self]
        )
        return SignInService(client: client, sessionManager: SessionManager(client: client, cookieStorage: storage))
    }

    private func makeOAuthFixture() throws -> (
        service: SignInService,
        coordinator: NativeOAuthAuthorizationCoordinator
    ) {
        let client = try makeClient()
        let storage = HTTPCookieStorage()
        let sessionManager = SessionManager(client: client, cookieStorage: storage)
        return try (
            SignInService(client: client, sessionManager: sessionManager),
            NativeOAuthAuthorizationCoordinator(
                client: client,
                sessionManager: sessionManager,
                store: NativeOAuthAuthorizationStore(
                    defaults: XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
                )
            )
        )
    }

    private func oauthCapabilities() -> Data {
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

}

private final class EmailOTPViewFailingURLProtocol: URLProtocol {
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        client?.urlProtocol(self, didFailWithError: URLError(.notConnectedToInternet))
    }

    override func stopLoading() {}
}
