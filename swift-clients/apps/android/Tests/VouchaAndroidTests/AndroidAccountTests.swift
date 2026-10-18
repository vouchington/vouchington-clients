import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaLocalization
import VouchaTestSupport
import XCTest

final class AndroidAccountTests: XCTestCase {
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

    func testSessionCookiesPersistSecurelyAndAreScopedToOrigin() async throws {
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
        await storage.flushPendingPersistence()

        XCTAssertNotNil(try secrets.values[XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))])
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

    func testDomainCookieIncludesSubdomainsAndMaxAgePersistsAsAbsoluteExpiry() async throws {
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
        await storage.flushPendingPersistence()

        XCTAssertEqual(try storage.cookies(for: XCTUnwrap(URL(string: "https://api.voucha.ai")))?.map(\.name), ["dt"])
        let json = try XCTUnwrap(try secrets
            .values[XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))]?.data(using: .utf8))
        let records = try XCTUnwrap(JSONSerialization.jsonObject(with: json) as? [[String: Any]])
        XCTAssertNotNil(records.first?["expires"])
    }

    func testVersionZeroMaxAgeOverridesExpiresAndSnapshotDeadlineIsAbsolute() throws {
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "dt",
            .value: "device-secret",
            .domain: ".voucha.ai",
            .path: "/",
            .version: "0",
            .expires: Date(timeIntervalSince1970: 4_102_444_800),
            .maximumAge: "60"
        ]))
        let properties = try XCTUnwrap(cookie.properties)
        let createdSeconds = try XCTUnwrap(properties[HTTPCookiePropertyKey("Created")] as? NSNumber)
        let expectedExpiry = Date(timeIntervalSinceReferenceDate: createdSeconds.doubleValue)
            .addingTimeInterval(60)

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

    func testVersionZeroMaxAgeZeroIsNotPersistedDespiteFutureExpires() async throws {
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "expired",
            .value: "no-session",
            .domain: "voucha.ai",
            .path: "/",
            .version: "0",
            .expires: Date(timeIntervalSince1970: 4_102_444_800),
            .maximumAge: "0"
        ]))

        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        storage.setCookie(cookie)
        await storage.flushPendingPersistence()

        let persisted = try XCTUnwrap(try secrets
            .values[XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))]?.data(using: .utf8))
        let records = try JSONDecoder().decode([AndroidSessionCookieStorage.CookieRecord].self, from: persisted)
        XCTAssertTrue(records.isEmpty)
        let restored = AndroidSessionCookieStorage(secrets: secrets)
        XCTAssertTrue(restored.cookies?.isEmpty ?? true)
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

    func testDeletingSessionCookieRemovesItFromDurableStorage() async throws {
        let secrets = MemorySecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .name: "dt", .value: "device-secret", .domain: "voucha.ai", .path: "/"
        ]))
        storage.setCookie(cookie)

        storage.deleteCookie(cookie)
        await storage.flushPendingPersistence()

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

}
