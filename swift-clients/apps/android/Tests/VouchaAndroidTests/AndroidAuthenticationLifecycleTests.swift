import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import XCTest

final class AndroidAuthenticationLifecycleTests: XCTestCase {
    @MainActor
    func testHeldOTPRequestLocksEmailAndRejectsStaleTurnstileCallback() async throws {
        let host = "auth-\(UUID().uuidString.lowercased()).example.com"
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/auth/email-address/tokens")
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }

        let account = try makeAccount(host: host)
        account.email = "first@example.com"
        let challengeID = account.turnstileChallengeID
        account.turnstileTokenDidChange("single-use-token", challengeID: challengeID)
        let request = Task { await account.requestOTP() }

        await fulfillment(of: [started], timeout: 3)

        account.email = "second@example.com"
        XCTAssertEqual(account.email, "first@example.com")
        XCTAssertFalse(account.restartSignIn())
        account.turnstileTokenDidChange("late-token", challengeID: challengeID)
        XCTAssertEqual(account.turnstileToken, "single-use-token")

        let released = HeldAuthenticationURLProtocol.respond(
            host: host,
            statusCode: 503,
            body: Data("{}".utf8)
        )
        if !released { request.cancel() }
        await request.value
        XCTAssertTrue(released)
        XCTAssertFalse(account.otpRequested)
        XCTAssertNotNil(account.authError)
        XCTAssertEqual(account.email, "first@example.com")
        XCTAssertNil(account.turnstileToken)
        XCTAssertNotEqual(account.turnstileChallengeID, challengeID)

        account.turnstileTokenDidChange("stale-token", challengeID: challengeID)
        XCTAssertNil(account.turnstileToken)
    }

    @MainActor
    func testHeldOTPVerificationCannotAdvanceAfterAttemptToChangeEmail() async throws {
        let host = "auth-\(UUID().uuidString.lowercased()).example.com"
        let started = HeldAuthenticationURLProtocol.expectRequest(
            host: host,
            path: "/api/v1/auth/email-address/login"
        )
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }

        let account = try makeAccount(host: host)
        account.email = "first@example.com"
        account.otp = "123456"
        account.otpRequested = true
        let verification = Task { await account.verifyOTP() }

        await fulfillment(of: [started], timeout: 3)

        account.email = "second@example.com"
        XCTAssertEqual(account.email, "first@example.com")
        XCTAssertFalse(account.restartSignIn())

        let released = HeldAuthenticationURLProtocol.respond(
            host: host,
            statusCode: 200,
            body: Data(#"{"mfaRequired":true,"loginAttemptId":"first-account-mfa"}"#.utf8)
        )
        if !released { verification.cancel() }
        await verification.value
        XCTAssertTrue(released)
        XCTAssertEqual(account.email, "first@example.com")
        XCTAssertTrue(account.otpRequested)
        XCTAssertEqual(account.loginAttemptID, "first-account-mfa")
        XCTAssertFalse(account.isAuthenticating)
    }

    @MainActor
    func testRestartSignInClearsMFAStateAndRotatesTurnstileChallenge() {
        let account = AndroidAccount(cookieStorage: AndroidSessionCookieStorage(secrets: AuthenticationMemorySecrets()))
        account.email = "person@example.com"
        account.otpRequested = true
        account.otp = "123456"
        account.totp = "654321"
        account.loginAttemptID = "mfa-attempt"
        account.turnstileToken = "old-token"
        account.authError = .verbatim("previous error")
        let oldChallengeID = account.turnstileChallengeID

        XCTAssertTrue(account.restartSignIn())

        XCTAssertEqual(account.email, "person@example.com")
        XCTAssertFalse(account.otpRequested)
        XCTAssertEqual(account.otp, "")
        XCTAssertEqual(account.totp, "")
        XCTAssertNil(account.loginAttemptID)
        XCTAssertNil(account.turnstileToken)
        XCTAssertNil(account.authError)
        XCTAssertNotEqual(account.turnstileChallengeID, oldChallengeID)
    }

    @MainActor
    private func makeAccount(host: String) throws -> AndroidAccount {
        let baseURL = try XCTUnwrap(URL(string: "https://\(host)"))
        return AndroidAccount(
            config: AppConfig(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: AndroidSessionCookieStorage(secrets: AuthenticationMemorySecrets()),
            protocolClasses: [HeldAuthenticationURLProtocol.self]
        )
    }
}

private final class AuthenticationMemorySecrets: AndroidEndpointSecretStoring {
    func string(forKey _: String) throws -> String? {
        nil
    }

    func set(_: String, forKey _: String) throws {}
    func removeValue(forKey _: String) throws {}
}
