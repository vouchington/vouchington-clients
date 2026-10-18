import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

final class AndroidCookieDiscardRecoveryTests: XCTestCase {
    func testRepeatedReadFailurePreservesExplicitDiscardWithoutAutomaticMutation() throws {
        let secrets = CookieDiscardRecoverySecrets()
        let persistenceKey = try XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))
        secrets.seed("malformed snapshot", forKey: persistenceKey)
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        XCTAssertTrue(storage.persistenceStatus.unreadableSnapshotCanBeDiscarded)

        secrets.setReadFailure(true)
        XCTAssertFalse(storage.retryRestoreCookies())

        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertTrue(storage.persistenceStatus.unreadableSnapshotCanBeDiscarded)
        XCTAssertEqual(secrets.counts().removals, 0)
        XCTAssertEqual(secrets.counts().writes, 0)
        XCTAssertEqual(secrets.snapshotValue(forKey: persistenceKey), "malformed snapshot")
    }

    @MainActor
    func testMalformedSnapshotNeedsSuccessfulExplicitDeletionBeforeSignIn() async throws {
        try await assertExplicitDeletionBeforeSignIn(readFailure: false)
    }

    @MainActor
    func testPermanentReadFailureNeedsSuccessfulExplicitDeletionBeforeSignIn() async throws {
        try await assertExplicitDeletionBeforeSignIn(readFailure: true)
    }

    @MainActor
    private func assertExplicitDeletionBeforeSignIn(readFailure: Bool) async throws {
        let fixture = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [fixture])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(fixture, root: root))
        let host = "cookie-discard-\(UUID().uuidString.lowercased()).voucha.ai"
        let secrets = CookieDiscardRecoverySecrets()
        let persistenceKey = try XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))
        let malformed = "not a cookie snapshot"
        secrets.seed(malformed, forKey: persistenceKey)
        secrets.setRemovalFailure(true)
        secrets.setReadFailure(readFailure)
        secrets.seed("other-origin-session", forKey: "voucha.session.cookies.https://other.example:443")
        secrets.seed("endpoint-secret", forKey: "endpoint-api-key")
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
        await account.retryCookiePersistenceOrRestoration()
        XCTAssertTrue(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)

        let firstDiscard = await account.discardUnreadableCookieSnapshot()
        XCTAssertFalse(firstDiscard)
        XCTAssertTrue(account.isRestoring)
        XCTAssertTrue(storage.requiresRestorationRetry)
        XCTAssertTrue(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)
        XCTAssertEqual(secrets.snapshotValue(forKey: persistenceKey), malformed)
        XCTAssertTrue(probe.observations().paths.isEmpty)
        XCTAssertEqual(secrets.counts().writes, 0)

        secrets.setRemovalFailure(false)
        let retriedDiscard = await account.discardUnreadableCookieSnapshot()
        XCTAssertTrue(retriedDiscard)

        XCTAssertTrue(account.isRestoring)
        XCTAssertFalse(storage.requiresRestorationRetry)
        XCTAssertFalse(storage.persistenceStatus.enabled)
        XCTAssertFalse(account.cookiePersistenceNotice.canDiscardUnreadableSnapshot)
        XCTAssertTrue(probe.observations().paths.isEmpty)
        XCTAssertNil(secrets.snapshotValue(forKey: persistenceKey))
        XCTAssertEqual(secrets.counts().removals, 1)
        XCTAssertEqual(
            secrets.snapshotValue(forKey: "voucha.session.cookies.https://other.example:443"),
            "other-origin-session"
        )
        XCTAssertEqual(secrets.snapshotValue(forKey: "endpoint-api-key"), "endpoint-secret")

        try await assertFreshBootstrapAfterDiscard(account: account, secrets: secrets, probe: probe)

    }

    @MainActor
    private func assertFreshBootstrapAfterDiscard(
        account: AndroidAccount,
        secrets: CookieDiscardRecoverySecrets,
        probe: CookieDiscardRequestProbe
    ) async throws {
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
