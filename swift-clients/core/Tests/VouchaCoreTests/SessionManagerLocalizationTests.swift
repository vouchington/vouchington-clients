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
        XCTAssertEqual(sessionManager.uiLocale, "es")
    }

    func testRefreshRetainsAccountType() async throws {
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
        }
    }

    func testUnauthorizedRefreshClearsSynchronizedLocaleAndIdentity() async throws {
        let sessionManager = makeSessionManager()
        try sessionManager.synchronize(with: identity(uiLocale: "fr"))
        MockURLProtocol.handlers["/api/v1/my/identity"] = (Data("{}".utf8), 401)

        let refreshed = await sessionManager.refresh()

        XCTAssertFalse(refreshed)
        XCTAssertFalse(sessionManager.isSignedIn)
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

    private func makeSessionManager(
        cookieStorage: HTTPCookieStorage = IsolatedHTTPCookieStorage.make()
    ) -> SessionManager {
        let client = APIClient(
            config: AppConfig(
                baseURL: URL(string: "http://localhost:2999")!,
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: cookieStorage,
            protocolClasses: [MockURLProtocol.self]
        )
        return SessionManager(client: client, cookieStorage: cookieStorage)
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
