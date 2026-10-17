import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidAuthFlowTests: XCTestCase {
    @MainActor
    func testEmailOtpMfaSessionRestoreAndSignOutUseAndroidClientAndFixtureIdentity() async throws {
        AndroidAuthURLProtocol.resetRequests()
        try installAndroidIdentityFixture()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let secrets = MemorySecrets()
        let cookies = AndroidSessionCookieStorage(secrets: secrets)
        let account = try AndroidAccount(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "https://voucha.ai")),
                turnstileSiteKey: "public-site-key"
            ),
            cookieStorage: cookies,
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        XCTAssertEqual(account.config.turnstileSiteKey, "public-site-key")
        account.email = "person@example.com"
        account.turnstileToken = "challenge-token"

        await account.requestOTP()
        account.otp = "123456"
        await account.verifyOTP()
        account.totp = "654321"
        await account.verifyMFA()

        XCTAssertNil(account.authError)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNotNil(account.session.currentUserId)
        try assertAuthenticationRequests(AndroidAuthURLProtocol.requestSnapshot())

        try cookies.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "session", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))
        let restoredAccount = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        await restoredAccount.restore()
        XCTAssertTrue(restoredAccount.session.isSignedIn)
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().last?.url?.path, "/api/v1/my/identity")
        AndroidAuthURLProtocol.setLogoutStatusCode(503)
        await restoredAccount.signOut()

        XCTAssertFalse(restoredAccount.session.isSignedIn)
        XCTAssertTrue(restoredAccount.cookieStorage.cookies?.isEmpty ?? true)
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().last?.url?.path, "/api/v1/auth/logout")

    }

    @MainActor
    func testLateOldClientPersistenceCannotRestoreCookiesAfterSignOut() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "session", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: storage,
            protocolClasses: [AndroidAuthURLProtocol.self]
        )

        await account.signOut()
        try storage.cookieStorage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "late-response", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))
        storage.persistCookies()

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

    @MainActor
    func testFailedPreSignOutCleanupKeepsSessionAndCookiePersistenceActive() async throws {
        AndroidAuthURLProtocol.resetRequests()
        try installAndroidIdentityFixture()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "session", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "dt", .value: "device", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: storage,
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        await account.restore()
        XCTAssertTrue(account.session.isSignedIn)
        await account.session.setBeforeSignOut { _ in throw URLError(.cancelled) }

        await account.signOut()
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "still-active", .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ])))

        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertTrue(secrets.values["voucha.session.cookies"]?.contains("still-active") == true)
    }
}

private func assertAuthenticationRequests(_ requests: [URLRequest]) throws {
    XCTAssertEqual(requests.map { $0.url?.path }, [
        "/api/v1/auth/email-address/tokens",
        "/api/v1/auth/email-address/login",
        "/api/v1/auth/mfa/totp/verification",
        "/api/v1/my/identity"
    ])
    XCTAssertTrue(try String(data: XCTUnwrap(requests.first?.httpBody), encoding: .utf8)?
        .contains("challenge-token") == true)
    XCTAssertTrue(requests.allSatisfy { $0.value(forHTTPHeaderField: "x-voucha-platform") == "android" })
}

private func installAndroidIdentityFixture() throws {
    let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
    let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
    try AndroidAuthURLProtocol.setIdentity(
        Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
    )
}
