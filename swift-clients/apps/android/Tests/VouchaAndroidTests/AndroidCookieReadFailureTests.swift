import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidCookieReadFailureTests: XCTestCase {
    @MainActor
    func testReadFailurePreservesSnapshotAndExplicitRetryRestoresBeforeIdentityRequest() async throws {
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        let host = "cookie-read-\(UUID().uuidString.lowercased()).voucha.ai"
        let secrets = CookieReadFailureSecrets()
        let rawSnapshot = try CookieReadFailureTestFixture.cookieSnapshot(value: "original-session", domain: host)
        secrets.seed(rawSnapshot, forKey: "voucha.session.cookies")
        secrets.setReadFailure(true)

        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let probe = CookieReadRetryRequestProbe()
        probe.reset(storage: storage, identity: identity)
        CookieReadRetryURLProtocol.registry.register(probe, forHost: host)
        defer { CookieReadRetryURLProtocol.registry.unregister(forHost: host) }
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://\(host)"))),
            cookieStorage: storage,
            protocolClasses: [CookieReadRetryURLProtocol.self]
        )

        await account.restore()

        XCTAssertTrue(account.isRestoring)
        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertFalse(storage.persistenceStatus.enabled)
        XCTAssertNotNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertTrue(storage.cookies?.isEmpty ?? true)
        XCTAssertTrue(probe.requestPaths().isEmpty)
        XCTAssertEqual(secrets.snapshotValue(forKey: "voucha.session.cookies"), rawSnapshot)
        XCTAssertEqual(secrets.writeCount(), 0)

        await account.retryCookiePersistenceOrRestoration()

        XCTAssertTrue(account.isRestoring)
        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertNotNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertTrue(probe.requestPaths().isEmpty)
        XCTAssertEqual(secrets.snapshotValue(forKey: "voucha.session.cookies"), rawSnapshot)
        XCTAssertEqual(secrets.writeCount(), 0)

        secrets.setReadFailure(false)
        await account.retryCookiePersistenceOrRestoration()

        XCTAssertFalse(account.isRestoring)
        XCTAssertFalse(storage.requiresRestorationRetry)
        XCTAssertTrue(storage.persistenceStatus.enabled)
        XCTAssertNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertEqual(probe.requestPaths(), ["/api/v1/my/identity"])
        XCTAssertEqual(probe.cookieValuesAtRequests(), [["original-session"]])
        let persisted = try XCTUnwrap(secrets.snapshotValue(forKey: "voucha.session.cookies"))
        XCTAssertEqual(
            try JSONSerialization.jsonObject(with: Data(persisted.utf8)) as? NSArray,
            try JSONSerialization.jsonObject(with: Data(rawSnapshot.utf8)) as? NSArray
        )
        // APIClient persists the restored jar after the successful identity response.
        XCTAssertEqual(secrets.writeCount(), 1)
    }

    @MainActor
    func testMalformedSnapshotBlocksBootstrapAndCannotBeOverwritten() async throws {
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        let host = "cookie-read-\(UUID().uuidString.lowercased()).voucha.ai"
        let secrets = CookieReadFailureSecrets()
        let malformed = "not a cookie snapshot"
        secrets.seed(malformed, forKey: "voucha.session.cookies")
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let probe = CookieReadRetryRequestProbe()
        probe.reset(storage: storage, identity: identity)
        CookieReadRetryURLProtocol.registry.register(probe, forHost: host)
        defer { CookieReadRetryURLProtocol.registry.unregister(forHost: host) }
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://\(host)"))),
            cookieStorage: storage,
            protocolClasses: [CookieReadRetryURLProtocol.self]
        )

        await account.restore()
        await account.retryCookiePersistenceOrRestoration()

        XCTAssertTrue(account.isRestoring)
        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertNotNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertTrue(probe.requestPaths().isEmpty)
        XCTAssertEqual(secrets.snapshotValue(forKey: "voucha.session.cookies"), malformed)
        XCTAssertEqual(secrets.writeCount(), 0)
    }

    func testPermanentDisableCannotBeUndoneByRestoreRetry() throws {
        let secrets = CookieReadFailureSecrets()
        let rawSnapshot = try CookieReadFailureTestFixture.cookieSnapshot(value: "original-session")
        secrets.seed(rawSnapshot, forKey: "voucha.session.cookies")
        secrets.setReadFailure(true)
        let storage = AndroidSessionCookieStorage(secrets: secrets)

        storage.disablePersistenceWrites()

        XCTAssertFalse(storage.retryRestoreCookies())
        XCTAssertFalse(storage.persistenceStatus.enabled)
        try storage.setCookie(CookieReadFailureTestFixture.sessionCookie(value: "late-response"))
        XCTAssertEqual(secrets.snapshotValue(forKey: "voucha.session.cookies"), rawSnapshot)
        XCTAssertEqual(secrets.writeCount(), 0)
    }

}
