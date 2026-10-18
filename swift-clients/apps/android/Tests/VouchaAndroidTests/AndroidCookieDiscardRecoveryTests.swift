import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidCookieDiscardRecoveryTests: XCTestCase {
    func testTransientRetryReadFailureClearsStaleDiscardEligibility() throws {
        let secrets = CookieDiscardRecoverySecrets()
        let persistenceKey = try XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))
        secrets.seed("malformed snapshot", forKey: persistenceKey)
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        XCTAssertTrue(storage.persistenceStatus.unreadableSnapshotCanBeDiscarded)

        secrets.setReadFailure(true)
        XCTAssertFalse(storage.retryRestoreCookies())

        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertFalse(storage.persistenceStatus.unreadableSnapshotCanBeDiscarded)
        XCTAssertFalse(storage.discardUnreadableSnapshot())
        XCTAssertEqual(secrets.snapshotValue(forKey: persistenceKey), "malformed snapshot")
    }

    @MainActor
    func testMalformedSnapshotNeedsSuccessfulExplicitDeletionBeforeSignIn() async throws {
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        let host = "cookie-discard-\(UUID().uuidString.lowercased()).voucha.ai"
        let secrets = CookieDiscardRecoverySecrets()
        let persistenceKey = try XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))
        let malformed = "not a cookie snapshot"
        secrets.seed(malformed, forKey: persistenceKey)
        secrets.setRemovalFailure(true)
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let probe = CookieDiscardRequestProbe(secrets: secrets, identity: identity, key: persistenceKey)
        CookieDiscardURLProtocol.registry.register(probe, forHost: host)
        defer { CookieDiscardURLProtocol.registry.unregister(forHost: host) }
        let account = try AndroidAccount(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://\(host)"))),
            cookieStorage: storage,
            protocolClasses: [CookieDiscardURLProtocol.self]
        )

        await account.restore()
        XCTAssertTrue(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)

        XCTAssertFalse(account.discardUnreadableCookieSnapshot())
        XCTAssertTrue(account.isRestoring)
        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertTrue(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)
        XCTAssertEqual(secrets.snapshotValue(forKey: persistenceKey), malformed)
        XCTAssertTrue(probe.observations().paths.isEmpty)
        XCTAssertEqual(secrets.counts().writes, 0)

        secrets.setRemovalFailure(false)
        XCTAssertTrue(account.discardUnreadableCookieSnapshot())

        XCTAssertTrue(account.isRestoring)
        XCTAssertFalse(storage.requiresRestorationRetry)
        XCTAssertFalse(storage.persistenceStatus.enabled)
        XCTAssertFalse(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)
        XCTAssertTrue(probe.observations().paths.isEmpty)
        XCTAssertNil(secrets.snapshotValue(forKey: persistenceKey))
        XCTAssertEqual(secrets.counts().removals, 1)

        let freshStorage = AndroidSessionCookieStorage(secrets: secrets)
        let freshAccount = try AndroidAccount(
            config: account.config,
            cookieStorage: freshStorage,
            protocolClasses: [CookieDiscardURLProtocol.self]
        )
        await freshAccount.restore()

        XCTAssertFalse(freshAccount.isRestoring)
        XCTAssertEqual(probe.observations().paths, ["/api/v1/my/identity"])
        XCTAssertEqual(probe.observations().snapshots, [nil])
    }
}
