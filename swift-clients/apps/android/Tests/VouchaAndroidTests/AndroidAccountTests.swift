import Foundation
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidAccountTests: XCTestCase {
    @MainActor
    func testExpiredOrFailedTurnstileChallengeClearsPreviouslyAcceptedToken() {
        let account = AndroidAccount(cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()))
        account.turnstileTokenDidChange("valid-token")
        XCTAssertEqual(account.turnstileToken, "valid-token")

        account.turnstileTokenDidChange(nil)

        XCTAssertNil(account.turnstileToken)
        XCTAssertNotNil(account.authError)
        account.turnstileTokenDidChange("replacement-token")
        XCTAssertEqual(account.turnstileToken, "replacement-token")
        XCTAssertNil(account.authError)
    }

    @MainActor
    func testFailedOTPRequestConsumesTurnstileToken() async throws {
        AndroidAuthURLProtocol.resetRequests()
        AndroidAuthURLProtocol.setOTPStatusCode(429)
        defer { AndroidAuthURLProtocol.resetRequests() }
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: AndroidSessionCookieStorage(secrets: MemorySecrets()),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        account.email = "person@example.com"
        account.turnstileToken = "single-use-token"

        await account.requestOTP()

        XCTAssertNil(account.turnstileToken)
        XCTAssertFalse(account.otpRequested)
        XCTAssertNotNil(account.authError)
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().map { $0.url?.path }, [
            "/api/v1/auth/email-address/tokens"
        ])
    }

    func testSessionCookiesPersistSecurelyAndAreScopedToOrigin() throws {
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "st",
            .value: "session-secret",
            .domain: "voucha.ai",
            .path: "/",
            .secure: "TRUE",
            .expires: Date(timeIntervalSinceNow: 600)
        ]))

        storage.setCookie(cookie)

        XCTAssertNotNil(secrets.values["voucha.session.cookies"])
        let restored = AndroidSessionCookieStorage(secrets: secrets)
        XCTAssertEqual(
            try restored.cookies(for: XCTUnwrap(URL(string: "https://voucha.ai/api/v1/my/identity")))?.map(\.name),
            ["st"]
        )
        XCTAssertTrue(try restored.cookies(for: XCTUnwrap(URL(string: "http://voucha.ai/api/v1/my/identity")))?
            .isEmpty ?? true)
        XCTAssertTrue(try restored.cookies(for: XCTUnwrap(URL(string: "https://api.voucha.ai/")))?.isEmpty ?? true)
        XCTAssertTrue(try restored.cookies(for: XCTUnwrap(URL(string: "https://example.com/")))?.isEmpty ?? true)
    }

    func testDomainCookieIncludesSubdomainsAndMaxAgePersistsAsAbsoluteExpiry() throws {
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "dt",
            .value: "device-secret",
            .domain: ".voucha.ai",
            .path: "/",
            .maximumAge: "60"
        ]))

        storage.setCookie(cookie)

        XCTAssertEqual(try storage.cookies(for: XCTUnwrap(URL(string: "https://api.voucha.ai")))?.map(\.name), ["dt"])
        let json = try XCTUnwrap(secrets.values["voucha.session.cookies"]?.data(using: .utf8))
        let records = try XCTUnwrap(JSONSerialization.jsonObject(with: json) as? [[String: Any]])
        XCTAssertNotNil(records.first?["expires"])
    }

    func testCookieSnapshotsKeepFoundationResolvedMaxAgeExpiryAbsolute() throws {
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "dt",
            .value: "device-secret",
            .domain: ".voucha.ai",
            .path: "/",
            .expires: Date(timeIntervalSince1970: 4_102_444_800),
            .maximumAge: "60"
        ]))
        let expectedExpiry = try XCTUnwrap(cookie.expiresDate)

        let firstSnapshot = try XCTUnwrap(AndroidSessionCookieStorage.CookieRecord(cookie: cookie))
        let encodedSnapshot = try JSONEncoder().encode(firstSnapshot)
        let restoredRecord = try JSONDecoder().decode(
            AndroidSessionCookieStorage.CookieRecord.self,
            from: encodedSnapshot
        )
        let restoredCookie = try XCTUnwrap(restoredRecord.cookie)
        let laterSnapshot = try XCTUnwrap(AndroidSessionCookieStorage.CookieRecord(cookie: restoredCookie))

        XCTAssertLessThan(expectedExpiry, Date().addingTimeInterval(120))
        XCTAssertEqual(firstSnapshot.expires, expectedExpiry)
        XCTAssertEqual(laterSnapshot.expires, expectedExpiry)
    }

    func testMaxAgeZeroDeletesCookieDespiteFutureExpires() throws {
        let storage = AndroidSessionCookieStorage(secrets: MemorySecrets())
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "expired",
            .value: "no-session",
            .domain: "voucha.ai",
            .path: "/",
            .expires: Date(timeIntervalSinceNow: 3_600),
            .maximumAge: "0"
        ]))

        storage.setCookie(cookie)

        XCTAssertTrue(storage.cookies?.isEmpty ?? true)
        let url = try XCTUnwrap(URL(string: "https://voucha.ai"))
        XCTAssertTrue(storage.cookies(for: url)?.isEmpty ?? true)
    }

    func testDeletingSessionCookieRemovesItFromDurableStorage() throws {
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "dt", .value: "device-secret", .domain: "voucha.ai", .path: "/"
        ]))
        storage.setCookie(cookie)

        storage.deleteCookie(cookie)

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

}
