import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
@testable import VouchaModels
import XCTest

@MainActor
final class SessionManagerLocalizationTests: XCTestCase {
    override func setUp() {
        super.setUp()
        MockURLProtocol.handlers = [:]
    }

    func testRefreshRestoresLocaleAndSignedInIdentity() async throws {
        MockURLProtocol.handlers["/api/v1/my/identity"] = try (
            identityEnvelope(uiLocale: "es"),
            200
        )
        let sessionManager = makeSessionManager()

        let refreshed = await sessionManager.refresh()

        XCTAssertTrue(refreshed)
        XCTAssertTrue(sessionManager.isSignedIn)
        XCTAssertEqual(sessionManager.currentUserId, "user-abc")
        XCTAssertEqual(sessionManager.currentUserRoles, ["user"])
        XCTAssertNil(sessionManager.currentUserAccountType)
        XCTAssertTrue(sessionManager.canCastPublicVotes)
        XCTAssertEqual(sessionManager.uiLocale, "es")
    }

    func testRefreshUsesEveryAccountClassificationForPublicVoteEligibility() async throws {
        let cases: [AccountType?] = [.official, .system, .aiAgent, nil]
        for accountType in cases {
            MockURLProtocol.handlers["/api/v1/my/identity"] = try (
                identityEnvelope(uiLocale: "en", accountType: accountType?.rawValue),
                200
            )
            let sessionManager = makeSessionManager()

            let refreshed = await sessionManager.refresh()

            XCTAssertTrue(refreshed)
            XCTAssertEqual(sessionManager.currentUserAccountType, accountType)
            XCTAssertEqual(sessionManager.canCastPublicVotes, accountType == nil)
        }
    }

    func testUnauthorizedRefreshClearsSynchronizedLocaleAndIdentity() async throws {
        let sessionManager = makeSessionManager()
        try sessionManager.synchronize(with: identity(uiLocale: "fr"))
        MockURLProtocol.handlers["/api/v1/my/identity"] = (Data("{}".utf8), 401)

        let refreshed = await sessionManager.refresh()

        XCTAssertFalse(refreshed)
        XCTAssertFalse(sessionManager.isSignedIn)
        XCTAssertFalse(sessionManager.canCastPublicVotes)
        XCTAssertNil(sessionManager.currentUserId)
        XCTAssertTrue(sessionManager.currentUserRoles.isEmpty)
        XCTAssertNil(sessionManager.uiLocale)
    }

    func testSignOutClearsCookiesAndSynchronizedLocaleWhenRequestFails() async throws {
        let cookieStorage = IsolatedHTTPCookieStorage.make()
        let sessionManager = makeSessionManager(cookieStorage: cookieStorage)
        try sessionManager.synchronize(with: identity(uiLocale: "pt"))
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .domain: "localhost",
            .path: "/",
            .name: "st",
            .value: "session-token"
        ]))
        cookieStorage.setCookie(cookie)
        MockURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 500)

        await sessionManager.signOut()

        XCTAssertFalse(sessionManager.isSignedIn)
        XCTAssertNil(sessionManager.uiLocale)
        XCTAssertTrue(cookieStorage.cookies?.isEmpty ?? true)
    }

    func testSignOutWaitsForDeviceGrantCleanupBeforeLosingIdentity() async throws {
        let sessionManager = makeSessionManager()
        try sessionManager.synchronize(with: identity(uiLocale: "en"))
        MockURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 200)
        let cleanupStarted = expectation(description: "device cleanup started")
        var releaseCleanup: CheckedContinuation<Void, Never>?
        sessionManager.setBeforeSignOut { accountId in
            XCTAssertEqual(accountId, "user-abc")
            cleanupStarted.fulfill()
            await withCheckedContinuation { releaseCleanup = $0 }
        }

        let signOut = Task { await sessionManager.signOut() }
        await fulfillment(of: [cleanupStarted], timeout: 2)
        XCTAssertEqual(sessionManager.currentUserId, "user-abc")
        XCTAssertEqual(sessionManager.uiLocale, "en")
        releaseCleanup?.resume()
        await signOut.value

        XCTAssertFalse(sessionManager.isSignedIn)
        XCTAssertNil(sessionManager.currentUserId)
    }

    func testFailedDeviceGrantCleanupLeavesSignOutRetryable() async throws {
        let sessionManager = makeSessionManager()
        try sessionManager.synchronize(with: identity(uiLocale: "fr"))
        MockURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 200)
        sessionManager.setBeforeSignOut { _ in throw DeviceCleanupError.failed }

        await sessionManager.signOut()

        XCTAssertEqual(sessionManager.currentUserId, "user-abc")
        XCTAssertEqual(sessionManager.uiLocale, "fr")
        XCTAssertNotNil(sessionManager.lastSignOutErrorMessage)
        sessionManager.setBeforeSignOut { accountId in
            XCTAssertEqual(accountId, "user-abc")
        }
        await sessionManager.signOut()
        XCTAssertNil(sessionManager.currentUserId)
        XCTAssertNil(sessionManager.lastSignOutErrorMessage)
    }

    func testPersistedClearRunsAfterJarClearEvenWhenLogoutFails() async throws {
        let jar = IsolatedHTTPCookieStorage.make()
        let recorder = CookiePersistenceRecorder(jar: jar)
        let manager = makeSessionManager(cookieStorage: jar, clearPersistedCookies: { recorder.snapshot() })
        try manager.synchronize(with: identity(uiLocale: "en"))
        try jar.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .domain: "localhost", .path: "/", .name: "st", .value: "private-session"
        ])))
        MockURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 500)

        await manager.signOut()

        XCTAssertEqual(recorder.snapshots, [[:]])
        XCTAssertFalse(manager.isSignedIn)
    }

    func testFailedLogoutClearsTheAPIClientsCanonicalJarWhenTheInjectedJarDiffers() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        let activeJar = try XCTUnwrap(configuration.httpCookieStorage)
        let separateJar = IsolatedHTTPCookieStorage.make()
        let recorder = CookiePersistenceRecorder(jar: activeJar)
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: separateJar, protocolClasses: [MockURLProtocol.self],
            bootstrapSession: false, sessionConfiguration: configuration
        )
        let manager = SessionManager(
            client: client, cookieStorage: separateJar, clearPersistedCookies: { recorder.snapshot() }
        )
        try manager.synchronize(with: identity(uiLocale: "en"))
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .domain: "localhost", .path: "/", .name: "st", .value: "private-session"
        ]))
        activeJar.setCookie(cookie)
        separateJar.setCookie(cookie)
        MockURLProtocol.handlers["/api/v1/auth/logout"] = (Data("{}".utf8), 500)

        await manager.signOut()

        XCTAssertTrue(activeJar.cookies?.isEmpty ?? true)
        XCTAssertTrue(separateJar.cookies?.isEmpty ?? true)
        XCTAssertEqual(recorder.snapshots, [[:]])
        XCTAssertFalse(manager.isSignedIn)
    }

    func testFailedPreSignOutCleanupDoesNotClearPersistedCookies() async throws {
        let jar = IsolatedHTTPCookieStorage.make()
        let recorder = CookiePersistenceRecorder(jar: jar)
        let manager = makeSessionManager(cookieStorage: jar, clearPersistedCookies: { recorder.snapshot() })
        try manager.synchronize(with: identity(uiLocale: "en"))
        manager.setBeforeSignOut { _ in throw DeviceCleanupError.failed }

        await manager.signOut()

        XCTAssertTrue(recorder.snapshots.isEmpty)
        XCTAssertTrue(manager.isSignedIn)
        XCTAssertNotNil(manager.lastSignOutErrorMessage)
    }

    private func makeSessionManager(
        cookieStorage: HTTPCookieStorage = IsolatedHTTPCookieStorage.make(),
        clearPersistedCookies: (@Sendable () -> Void)? = nil
    ) -> SessionManager {
        let client = APIClient(
            config: AppConfig(
                baseURL: URL(string: "http://localhost:2999")!,
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: cookieStorage,
            protocolClasses: [MockURLProtocol.self]
        )
        return SessionManager(
            client: client,
            cookieStorage: cookieStorage,
            clearPersistedCookies: clearPersistedCookies
        )
    }

    private func identity(uiLocale: String) throws -> PrivateUser {
        try makeVouchaDecoder().decode(
            IdentityEnvelope.self,
            from: identityEnvelope(uiLocale: uiLocale)
        ).identity
    }

    private func identityEnvelope(uiLocale: String, accountType: String? = nil) throws -> Data {
        var envelope = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("swift.my.identity.default")
            ) as? [String: Any]
        )
        var identity = try XCTUnwrap(envelope["identity"] as? [String: Any])
        identity["ui_locale"] = uiLocale
        identity["account_type"] = accountType as Any? ?? NSNull()
        envelope["identity"] = identity
        return try JSONSerialization.data(withJSONObject: envelope)
    }
}

private struct IdentityEnvelope: Decodable {
    let identity: PrivateUser
}

private enum DeviceCleanupError: Error {
    case failed
}
