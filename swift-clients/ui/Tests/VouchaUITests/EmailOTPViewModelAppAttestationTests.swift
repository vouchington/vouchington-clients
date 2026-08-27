import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

private final class StubAppAttestProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    var generateKeyResult: Result<String, Error> = .success("stub-key-id")
    var attestKeyResult: Result<Data, Error> = .success(Data("attestation-blob".utf8))
    var generateAssertionResult: Result<Data, Error> = .success(Data("assertion-blob".utf8))

    func generateKey() async throws -> String {
        try generateKeyResult.get()
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        try attestKeyResult.get()
    }

    func generateAssertion(_: String, clientDataHash _: Data) async throws -> Data {
        try generateAssertionResult.get()
    }
}

@MainActor
final class EmailOTPViewModelAppAttestationTests: XCTestCase {
    private var provider: StubAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        provider = StubAppAttestProvider()
        // Unique service name prevents cross-test keychain pollution.
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/challenge"] =
            (Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/attest"] = (Data("{}".utf8), 200)
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    private func makeViewModel() throws -> EmailOTPViewModel {
        let apiClient = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
        let sessionManager = SessionManager(client: apiClient, cookieStorage: HTTPCookieStorage())
        let appAttestationService = AppAttestationService(client: apiClient, provider: provider, keyStore: keyStore)
        let service = SignInService(
            client: apiClient,
            sessionManager: sessionManager,
            appAttestationService: appAttestationService
        )
        return EmailOTPViewModel(signInService: service)
    }

    func testCanRequestCodeIsTrueWithoutTurnstileTokenWhenAppAttestIsSupported() throws {
        let vm = try makeViewModel()
        vm.email = "alice@example.com"

        XCTAssertNil(vm.turnstileToken)
        XCTAssertTrue(vm.canRequestCode)
    }

    func testCanRequestCodeRequiresTurnstileTokenWhenAppAttestIsUnsupported() throws {
        provider.isSupported = false
        let vm = try makeViewModel()
        vm.email = "alice@example.com"

        XCTAssertFalse(vm.canRequestCode)
        vm.turnstileToken = "captcha-token"
        XCTAssertTrue(vm.canRequestCode)
    }

    func testRequestCodeUsesAppAttestHeadersInsteadOfTurnstile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/auth/email-address/tokens"] = (Data("{}".utf8), 200)
        let vm = try makeViewModel()
        vm.email = "alice@example.com"

        await vm.requestCode()

        XCTAssertNil(vm.turnstileToken)
        XCTAssertEqual(vm.step, .enterCode)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/auth/email-address/tokens" })
    }

    func testRequestCodeFallsBackToTurnstileRequiredWhenAttestationFails() async throws {
        provider.attestKeyResult = .failure(NSError(domain: "test", code: 1))
        let vm = try makeViewModel()
        vm.email = "alice@example.com"

        await vm.requestCode()

        XCTAssertEqual(vm.step, .enterEmail)
        XCTAssertEqual(uiEnglish(vm.errorMessage), "Verification token required")
        XCTAssertTrue(
            CannedFeedURLProtocol.capturedURLs.allSatisfy { $0.path != "/api/v1/auth/email-address/tokens" }
        )
    }

    func testRequestCodeRetriesWithTurnstileWhenServerRejectsAppAttestBypass() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/auth/email-address/tokens"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0),
            (Data("{}".utf8), 200, 0)
        ]
        let vm = try makeViewModel()
        vm.email = "alice@example.com"
        vm.turnstileToken = "captcha-token"

        await vm.requestCode()

        XCTAssertNil(vm.errorMessage)
        XCTAssertEqual(vm.step, .enterCode)
        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/auth/email-address/tokens" }
        XCTAssertEqual(attempts.count, 2)
    }

    func testRequestCodeSurfacesTurnstileRequiredWhenServerRejectsBypassAndNoTokenAvailable() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/auth/email-address/tokens"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0)
        ]
        let vm = try makeViewModel()
        vm.email = "alice@example.com"

        await vm.requestCode()

        XCTAssertEqual(vm.step, .enterEmail)
        XCTAssertEqual(uiEnglish(vm.errorMessage), "Verification token required")
        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/auth/email-address/tokens" }
        XCTAssertEqual(attempts.count, 1)
    }
}
