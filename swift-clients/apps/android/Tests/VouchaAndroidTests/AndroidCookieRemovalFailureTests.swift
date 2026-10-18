import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidCookieRemovalFailureTests: XCTestCase {
    @MainActor
    func testFailedCookieRemovalBlocksAccountReplacementAndCanRetry() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        )
        let secrets = CookieRemovalFailureSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try sessionCookie(value: "original-session")
        storage.setCookie(cookie)
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: storage,
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        await account.restore()
        XCTAssertTrue(account.session.isSignedIn)
        let userID = account.session.currentUserId
        let originalValue = secrets.snapshotValue(forKey: "voucha.session.cookies")
        let requestsBeforeSignOut = AndroidAuthURLProtocol.requestSnapshot().count
        secrets.setRemovalFailure(true)

        let mayReplaceAccount = await account.signOut()

        XCTAssertFalse(mayReplaceAccount)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertEqual(account.session.currentUserId, userID)
        XCTAssertNotNil(account.session.lastSignOutErrorMessage)
        XCTAssertEqual(secrets.snapshotValue(forKey: "voucha.session.cookies"), originalValue)
        XCTAssertEqual(storage.cookies?.map(\.value), ["original-session"])
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().count, requestsBeforeSignOut)

        secrets.setRemovalFailure(false)
        let mayReplaceAfterRetry = await account.signOut()

        XCTAssertTrue(mayReplaceAfterRetry)
        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNil(account.session.lastSignOutErrorMessage)
        XCTAssertTrue(storage.cookies?.isEmpty ?? true)
        XCTAssertNil(secrets.snapshotValue(forKey: "voucha.session.cookies"))
        storage.cookieStorage.setCookie(cookie)
        storage.persistCookies()
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

    func testFailedRemovalLeavesPersistenceRetryableAndSuccessPreventsRestoration() throws {
        let secrets = CookieRemovalFailureSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.setCookie(sessionCookie(value: "original-session"))
        secrets.setRemovalFailure(true)

        XCTAssertThrowsError(try storage.clearPersistedCookies())
        try storage.setCookie(sessionCookie(value: "retryable-session"))
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value), ["retryable-session"])
        secrets.setRemovalFailure(false)

        try storage.clearPersistedCookies()
        try storage.setCookie(sessionCookie(value: "late-session"))

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
        XCTAssertNil(secrets.snapshotValue(forKey: "voucha.session.cookies"))
    }

    private func sessionCookie(value: String) throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}

private final class CookieRemovalFailureSecrets: AndroidEndpointSecretStoring {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var failRemoval = false

    func snapshotValue(forKey key: String) -> String? {
        lock.lock()
        defer { lock.unlock() }
        return values[key]
    }

    func setRemovalFailure(_ fails: Bool) {
        lock.lock()
        defer { lock.unlock() }
        failRemoval = fails
    }

    func string(forKey key: String) throws -> String? {
        snapshotValue(forKey: key)
    }

    func set(_ value: String, forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        if failRemoval { throw URLError(.cannotWriteToFile) }
        values.removeValue(forKey: key)
    }
}
