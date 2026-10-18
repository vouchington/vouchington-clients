import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import XCTest

final class AndroidSignInConfigurationTests: XCTestCase {
    @MainActor
    func testOTPRequestForwardsResolvedPresentationLocale() async throws {
        defer { AndroidAuthURLProtocol.resetRequests() }
        for (identifier, expected) in [("fr-CA", "fr"), ("de-DE", "en")] {
            AndroidAuthURLProtocol.resetRequests()
            let account = try AndroidAccount(
                config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://staging.example.com"))),
                cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()),
                protocolClasses: [AndroidAuthURLProtocol.self]
            )
            account.email = "person@example.com"
            account.turnstileToken = "single-use-token"

            await account.requestOTP(locale: Locale(identifier: identifier))

            XCTAssertTrue(account.otpRequested)
            let request = try XCTUnwrap(AndroidAuthURLProtocol.requestSnapshot().first)
            let body = try XCTUnwrap(request.httpBody)
            let payload = try XCTUnwrap(JSONSerialization.jsonObject(with: body) as? [String: Any])
            XCTAssertEqual(payload["ui_locale"] as? String, expected)
        }
    }

    func testChallengeOriginKeepsConfiguredDeploymentAndPortWithoutAPISuffixOrCredentials() throws {
        for (input, expected) in [
            ("https://voucha.ai/api/v1", "https://voucha.ai"),
            ("HTTPS://STAGING.EXAMPLE.COM/path", "https://staging.example.com"),
            ("https://staging.example.com:8443/api?key=value#fragment", "https://staging.example.com:8443"),
            ("http://localhost:8080/", "http://localhost:8080"),
            ("https://user:secret@self-hosted.example/api", "https://self-hosted.example")
        ] {
            XCTAssertEqual(try androidTurnstileOrigin(XCTUnwrap(URL(string: input))), expected)
        }
        XCTAssertNil(try androidTurnstileOrigin(XCTUnwrap(URL(string: "file:///tmp/challenge"))))
        XCTAssertNil(try androidTurnstileOrigin(XCTUnwrap(URL(string: "/relative"))))
    }
}
