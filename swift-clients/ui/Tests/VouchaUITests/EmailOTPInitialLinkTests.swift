import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EmailOTPInitialLinkTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
    }

    func testInitialEmailAndCodeRequiresExplicitVerification() async {
        CannedFeedURLProtocol.handlers["/api/v1/auth/email-address/login"] = (
            Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8),
            200
        )
        let viewModel = makeViewModel(initialEmail: "alice@example.com", initialCode: "123456")

        XCTAssertEqual(viewModel.step, .enterCode)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [])

        await viewModel.verifyCode()

        XCTAssertEqual(viewModel.step, .mfaChallenge(loginAttemptId: "attempt-1"))
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/auth/email-address/login"]
        )
    }

    private func makeViewModel(initialEmail: String, initialCode: String) -> EmailOTPViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        let service = SignInService(client: apiClient, sessionManager: sessionManager)
        return EmailOTPViewModel(
            signInService: service,
            initialEmail: initialEmail,
            initialCode: initialCode
        )
    }
}
