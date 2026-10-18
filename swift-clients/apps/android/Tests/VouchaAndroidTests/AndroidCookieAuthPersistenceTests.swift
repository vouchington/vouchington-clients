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
    func testIdentityDoesNotBecomeSignedInBeforeEncryptedCookieWriteSucceeds() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
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
        secrets.setWriteFailure(false)

        await account.restore()

        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNil(account.sessionRestoreError)
        XCTAssertNil(storage.persistenceStatus.errorMessage)
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value).sorted(), [
            "device-session", "member-session"
        ])
    }

    func testMFACompletionKeepsRecoveryVisibleUntilEncryptedCookiesAreStored() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        )
        let secrets = FailingCookieWriteSecrets()
        let account = AndroidAccount(
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        account.email = "person@example.com"
        account.turnstileToken = "challenge-token"
        await account.requestOTP()
        account.otp = "123456"
        await account.verifyOTP()
        account.totp = "654321"
        XCTAssertNotNil(account.loginAttemptID)
        secrets.setWriteFailure(true)

        await account.verifyMFA()

        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNotNil(account.sessionRestoreError)
        XCTAssertNotNil(account.cookieStorage.persistenceStatus.errorMessage)
        secrets.setWriteFailure(false)

        await account.restore()

        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNil(account.sessionRestoreError)
        XCTAssertNil(account.cookieStorage.persistenceStatus.errorMessage)
    }

    func testOTPAndMFAChallengeDoNotAdvanceAfterCookieWriteFailure() async {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
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
        account.otp = "123456"
        await account.verifyOTP()

        XCTAssertNil(account.loginAttemptID)
        XCTAssertNotNil(account.authError)
    }

    private func cookie(_ value: String, name: String = "st") throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: name, .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}
