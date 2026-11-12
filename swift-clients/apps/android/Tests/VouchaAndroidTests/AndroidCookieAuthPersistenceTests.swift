import Foundation
import VouchaCore
import VouchaTestSupport
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidCookieAuthPersistenceTests: XCTestCase {
    func testFailedRenewalCookieWriteRequiresFreshAuthenticationBeforeIdentityPublication() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try ContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: ContractRoot.requiredURL(fixture, root: root))
        )
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        secrets.setWriteFailure(true)
        try storage.cookieStorage.setCookie(cookie("device-session", name: "dt"))
        try storage.cookieStorage.setCookie(cookie("member-session"))
        let account = AndroidAccount(cookieStorage: storage, protocolClasses: [AndroidAuthURLProtocol.self])

        await account.restore()

        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNotNil(account.sessionRestoreError)
        XCTAssertNotNil(storage.persistenceStatus.errorMessage)
        let key = try XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))
        XCTAssertNil(secrets.snapshot()[key])
        XCTAssertTrue(
            storage.cookies?.isEmpty == true,
            "Consumed credentials cannot remain live after failed persistence"
        )
        secrets.setWriteFailure(false)
        await account.restore()
        XCTAssertFalse(account.session.isSignedIn, "Retry must not mint an authenticated session without credentials")
        XCTAssertNil(account.sessionRestoreError)
        XCTAssertNil(secrets.snapshot()[key])

        await account.retryCookiePersistence()
        try await signInAgain(account)

        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNil(storage.persistenceStatus.errorMessage)
        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value).sorted(),
            try androidRotatedCookieValues()
        )

    }

    func testMFACompletionCannotPublishIdentityWhenItsCredentialCookieWriteFails() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try ContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: ContractRoot.requiredURL(fixture, root: root))
        )
        let secrets = FailingCookieWriteSecrets()
        let account = AndroidAccount(
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        account.email = "person@example.com"
        account.turnstileToken = "challenge-token"
        await account.requestOTP()
        account.otp = "abcdefgh23"
        await account.verifyOTP()
        account.totp = "654321"
        XCTAssertNotNil(account.loginAttemptID)
        secrets.setWriteFailure(true)

        await account.verifyMFA()

        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNotNil(account.authError)
        XCTAssertNotNil(account.cookieStorage.persistenceStatus.errorMessage)
        XCTAssertTrue(account.cookieStorage.cookies?.isEmpty == true)
        secrets.setWriteFailure(false)
        await account.restore()
        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertTrue(account.restartSignIn())

        await account.retryCookiePersistence()
        try await signInAgain(account)

        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNil(account.sessionRestoreError)
        XCTAssertNil(account.cookieStorage.persistenceStatus.errorMessage)

    }

    func testOTPAndMFAChallengeDoNotAdvanceAfterCookieWriteFailure() async {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        AndroidAuthURLProtocol.setResponseCookie(
            "challenge=otp; Path=/; Secure",
            path: "/api/v1/auth/email-address/tokens"
        )
        AndroidAuthURLProtocol.setResponseCookie(
            "challenge=mfa; Path=/; Secure",
            path: "/api/v1/auth/email-address/login"
        )
        let secrets = FailingCookieWriteSecrets()
        let account = AndroidAccount(
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        account.email = "person@example.com"
        account.turnstileToken = "challenge-token"
        secrets.setWriteFailure(true)

        await account.requestOTP()

        XCTAssertFalse(account.otpRequested)
        XCTAssertNotNil(account.authError)
        secrets.setWriteFailure(false)
        account.turnstileToken = "new-challenge-token"
        await account.requestOTP()
        XCTAssertTrue(account.otpRequested)

        secrets.setWriteFailure(true)
        account.otp = "abcdefgh23"
        await account.verifyOTP()

        XCTAssertNil(account.loginAttemptID)
        XCTAssertNotNil(account.authError)
    }

    private func signInAgain(_ account: AndroidAccount) async throws {
        account.email = "person@example.com"
        account.turnstileToken = "fresh-challenge"
        await account.requestOTP()
        XCTAssertTrue(account.otpRequested)
        account.otp = "abcdefgh23"
        await account.verifyOTP()
        XCTAssertNotNil(account.loginAttemptID)
        account.totp = "654321"
        await account.verifyMFA()
        XCTAssertNil(account.authError)
    }

    private func cookie(_ value: String, name: String = "st") throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: name, .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}
