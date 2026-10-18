import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

final class AndroidCookieOriginTests: XCTestCase {
    func testNamespaceUsesCanonicalOriginWithoutCredentialsOrRouteMetadata() throws {
        let decorated = try url("HTTPS://user:secret@EXAMPLE.test/api/prefix?query=value#fragment")
        let canonical = try url("https://example.test:443/another-prefix")
        XCTAssertEqual(
            AndroidSessionCookieNamespace.key(for: decorated),
            try XCTUnwrap(AndroidSessionCookieNamespace.key(for: canonical))
        )
        XCTAssertEqual(
            try AndroidSessionCookieNamespace.key(for: url("http://example.test")),
            try XCTUnwrap(AndroidSessionCookieNamespace.key(for: url("http://example.test:80")))
        )
    }

    func testDifferentPortNeverRestoresPreviousServiceCookiesOrClearsItsSnapshot() async throws {
        let secrets = OriginCookieSecrets()
        let firstURL = try url("https://example.test:8443")
        let secondURL = try url("https://example.test:9443")
        let first = AndroidSessionCookieStorage(secrets: secrets, baseURL: firstURL)
        try first.setCookie(cookie("first"))
        await first.flushPendingPersistence()
        XCTAssertEqual(first.cookies?.map(\.value), ["first"])
        let second = AndroidSessionCookieStorage(secrets: secrets, baseURL: secondURL)
        XCTAssertTrue(second.cookies?.isEmpty ?? true)
        try second.setCookie(cookie("second"))
        await second.flushPendingPersistence()

        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets, baseURL: firstURL).cookies?.map(\.value),
            ["first"]
        )
        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets, baseURL: secondURL).cookies?.map(\.value),
            ["second"]
        )
        try await first.clearPersistedCookies()
        try first.setCookie(cookie("late-first"))
        first.persistCookies()
        await first.flushPendingPersistence()

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets, baseURL: firstURL).cookies?.isEmpty ?? true)
        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets, baseURL: secondURL).cookies?.map(\.value),
            ["second"]
        )
    }

    func testSchemeAndHostSeparateSnapshotsEvenWhenCookieDomainWouldMatch() async throws {
        let secrets = OriginCookieSecrets()
        let originalURL = try url("https://example.test")
        let original = AndroidSessionCookieStorage(secrets: secrets, baseURL: originalURL)
        try original.setCookie(cookie("original"))
        await original.flushPendingPersistence()
        for raw in ["http://example.test:443", "https://other.example.test"] {
            let isolated = try AndroidSessionCookieStorage(secrets: secrets, baseURL: url(raw))
            XCTAssertTrue(isolated.cookies?.isEmpty ?? true)
        }
        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets, baseURL: originalURL).cookies?.map(\.value),
            ["original"]
        )
    }

    func testUnattributableLegacySnapshotIsNeverReadImportedDeletedOrOverwritten() async throws {
        let secrets = OriginCookieSecrets()
        secrets.seedUnreadableLegacyValue()
        let storage = try AndroidSessionCookieStorage(secrets: secrets, baseURL: url("https://example.test"))

        XCTAssertTrue(storage.cookies?.isEmpty ?? true)
        XCTAssertFalse(storage.requiresRestorationRetry)
        XCTAssertTrue(storage.persistenceStatus.enabled)
        try storage.setCookie(cookie("new-session"))
        try await storage.clearPersistedCookies()
        XCTAssertFalse(secrets.readKeys().contains("voucha.session.cookies"))
        XCTAssertEqual(secrets.snapshot()["voucha.session.cookies"], "unreadable legacy snapshot")
    }

    func testInvalidOrNonHttpOriginsCannotCreatePersistentNamespaces() throws {
        for raw in ["file:///tmp/api", "/api", "https://example.test:0", "https://example.test:65536"] {
            XCTAssertNil(try AndroidSessionCookieNamespace.key(for: url(raw)))
        }
    }

    private func url(_ raw: String) throws -> URL {
        try XCTUnwrap(URL(string: raw))
    }

    private func cookie(_ value: String) throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .domain: ".example.test", .path: "/", .name: "st", .value: value, .secure: "TRUE",
            .expires: Date().addingTimeInterval(3_600)
        ]))
    }
}

private final class OriginCookieSecrets: AndroidEndpointSecretStoring, @unchecked Sendable {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var reads: [String] = []

    func seedUnreadableLegacyValue() {
        lock.lock()
        defer { lock.unlock() }
        values["voucha.session.cookies"] = "unreadable legacy snapshot"
    }

    func readKeys() -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return reads
    }

    func snapshot() -> [String: String] {
        lock.lock()
        defer { lock.unlock() }
        return values
    }

    func string(forKey key: String) throws -> String? {
        lock.lock()
        defer { lock.unlock() }
        reads.append(key)
        if key == "voucha.session.cookies" { throw URLError(.cannotDecodeContentData) }
        return values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values.removeValue(forKey: key)
    }
}
