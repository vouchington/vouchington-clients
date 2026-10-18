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
    func testVolatileDeletionWithoutDiskAcknowledgementNeverCompletesSignOut() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        try AndroidAuthURLProtocol.setIdentity(
            Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        )
        let secrets = VolatileCookieDeletionSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let cookie = try sessionCookie(value: "original-session")
        storage.setCookie(cookie)
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.ai"))),
            cookieStorage: storage,
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        await account.restore()
        await storage.flushPendingPersistence()
        XCTAssertTrue(account.session.isSignedIn)
        let requestsBeforeSignOut = AndroidAuthURLProtocol.requestSnapshot().count
        secrets.failDeletionCommit = true

        let firstSignOut = await account.signOut()
        XCTAssertFalse(firstSignOut)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertNotNil(account.session.lastSignOutErrorMessage)
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().count, requestsBeforeSignOut)
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
        XCTAssertEqual(
            AndroidSessionCookieStorage(secrets: secrets.reconstructProcess()).cookies?.map(\.value),
            ["original-session"]
        )

        secrets.failDeletionCommit = false
        let retriedSignOut = await account.signOut()
        XCTAssertTrue(retriedSignOut)
        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertTrue(
            AndroidSessionCookieStorage(secrets: secrets.reconstructProcess()).cookies?.isEmpty ?? true
        )
    }

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
        await storage.flushPendingPersistence()
        XCTAssertTrue(account.session.isSignedIn)
        let userID = account.session.currentUserId
        let originalValue = try secrets
            .snapshotValue(forKey: XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL)))
        let requestsBeforeSignOut = AndroidAuthURLProtocol.requestSnapshot().count
        secrets.setRemovalFailure(true)

        let mayReplaceAccount = await account.signOut()

        XCTAssertFalse(mayReplaceAccount)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertEqual(account.session.currentUserId, userID)
        XCTAssertNotNil(account.session.lastSignOutErrorMessage)
        XCTAssertEqual(
            try secrets
                .snapshotValue(forKey: XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))),
            originalValue
        )
        XCTAssertEqual(storage.cookies?.map(\.value), ["original-session"])
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().count, requestsBeforeSignOut)

        secrets.setRemovalFailure(false)
        let mayReplaceAfterRetry = await account.signOut()

        XCTAssertTrue(mayReplaceAfterRetry)
        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNil(account.session.lastSignOutErrorMessage)
        XCTAssertTrue(storage.cookies?.isEmpty ?? true)
        XCTAssertNil(try secrets
            .snapshotValue(forKey: XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))))
        storage.cookieStorage.setCookie(cookie)
        storage.persistCookies()
        await storage.flushPendingPersistence()
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

    func testFailedRemovalLeavesPersistenceRetryableAndSuccessPreventsRestoration() async throws {
        let secrets = CookieRemovalFailureSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.setCookie(sessionCookie(value: "original-session"))
        secrets.setRemovalFailure(true)

        do {
            try await storage.clearPersistedCookies()
            XCTFail("An unacknowledged cookie removal must fail")
        } catch {}
        try storage.setCookie(sessionCookie(value: "retryable-session"))
        await storage.flushPendingPersistence()
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value), ["retryable-session"])
        secrets.setRemovalFailure(false)

        try await storage.clearPersistedCookies()
        try storage.setCookie(sessionCookie(value: "late-session"))
        await storage.flushPendingPersistence()

        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
        XCTAssertNil(try secrets
            .snapshotValue(forKey: XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))))
    }

    private func sessionCookie(value: String) throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}
