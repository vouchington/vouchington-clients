import Foundation
import VouchaAPI
import VouchaAuth
import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EmailOTPViewModelTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeViewModel(protocolClasses: [AnyClass] = [EmailOTPFailingURLProtocol.self]) -> EmailOTPViewModel {
        let config = AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key")
        let cookieStorage = HTTPCookieStorage()
        let apiClient = APIClient(
            config: config,
            cookieStorage: cookieStorage,
            protocolClasses: protocolClasses
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: cookieStorage)
        let service = SignInService(client: apiClient, sessionManager: sessionManager)
        return EmailOTPViewModel(signInService: service)
    }

    func testInitialState() {
        let vm = makeViewModel()
        XCTAssertEqual(vm.step, .enterEmail)
        XCTAssertEqual(vm.email, "")
        XCTAssertEqual(vm.code, "")
        XCTAssertFalse(vm.isLoading)
        XCTAssertNil(vm.errorMessage)
        XCTAssertFalse(vm.didSucceed)
    }

    func testRequestCodeGuardsEmptyEmail() async {
        let vm = makeViewModel()
        vm.email = ""

        await vm.requestCode()

        XCTAssertEqual(vm.step, .enterEmail)
    }

    func testRequestCodeRequiresTurnstileToken() async {
        let vm = makeViewModel()
        vm.email = "alice@example.com"

        await vm.requestCode()

        XCTAssertEqual(vm.step, .enterEmail)
        XCTAssertEqual(uiEnglish(vm.errorMessage), "Verification token required")
    }

    func testRequestCodeSendsTurnstileToken() async {
        CannedFeedURLProtocol.handlers["/api/v1/auth/email-address/tokens"] = (
            Data(#"{"email_address":"alice@example.com"}"#.utf8),
            200
        )
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        vm.email = "alice@example.com"
        vm.turnstileToken = "captcha-token"
        vm.uiLocale = "es"

        await vm.requestCode()

        let otpRequest = zip(CannedFeedURLProtocol.capturedURLs, CannedFeedURLProtocol.capturedBodies)
            .first { url, _ in url.path == "/api/v1/auth/email-address/tokens" }
        XCTAssertNotNil(otpRequest)
        XCTAssertTrue(otpRequest?.1?.contains(#""ui_locale":"es""#) == true)
        XCTAssertEqual(vm.step, .enterCode)
    }

    func testMalformedEmailCodeNeverSendsARequest() async {
        let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
        vm.email = "alice@example.com"
        vm.step = .enterCode
        for code in ["123456", "ABCDEFGH01", "ABCDEFGH89", "ＡBCDEFGH23", "abcdefgh2 "] {
            vm.code = code
            XCTAssertFalse(vm.canVerifyCode, code)
            await vm.verifyCode()
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(vm.step, .enterCode)
        XCTAssertFalse(vm.didSucceed)
    }

    func testEmailVerificationFailuresStayEditableWithoutAutomaticRetry() async throws {
        let path = "/api/v1/auth/email-address/login"
        for (status, message) in [
            (400, "Invalid code"),
            (400, "Replaced code"),
            (400, "Purpose mismatch"),
            (429, "Quota exhausted")
        ] {
            CannedFeedURLProtocol.capturedURLs = []
            CannedFeedURLProtocol.handlers[path] = try (
                JSONSerialization.data(withJSONObject: ["message": message]), status
            )
            let vm = makeViewModel(protocolClasses: [CannedFeedURLProtocol.self])
            vm.email = "alice@example.com"
            vm.code = "abcdefgh23"
            vm.step = .enterCode

            await vm.verifyCode()

            XCTAssertEqual(vm.code, "ABCDEFGH23")
            XCTAssertEqual(vm.step, .enterCode)
            XCTAssertTrue(vm.canVerifyCode)
            XCTAssertFalse(vm.didSucceed)
            XCTAssertNotNil(vm.errorMessage)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [path])
        }
    }

    func testDefaultUiLocaleUsesPreferredUiLanguageRegionTags() {
        XCTAssertEqual(EmailOTPViewModel.defaultUiLocale(preferredLanguages: ["pt-BR"]), "pt")
    }

    func testNormalizeUiLocalePreservesSupportedRegionalTagsBeforeBaseFallback() {
        // Covers the full-tag branch before the production catalog adds regional locales.
        let supportedLocales: Set = ["pt", "pt-br"]

        XCTAssertEqual(
            EmailOTPViewModel.normalizeUiLocale("pt-BR", supportedUiLocales: supportedLocales),
            "pt-br"
        )
    }

    func testNormalizeUiLocaleNormalizesUnderscoresAndWhitespace() {
        XCTAssertEqual(EmailOTPViewModel.normalizeUiLocale("  fr_CA  "), "fr")
    }

    func testDefaultUiLocaleFallsThroughUnsupportedPreferredLanguages() {
        XCTAssertEqual(EmailOTPViewModel.defaultUiLocale(preferredLanguages: ["de-DE", "fr-FR"]), "fr")
    }
}

private final class EmailOTPFailingURLProtocol: URLProtocol {
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
