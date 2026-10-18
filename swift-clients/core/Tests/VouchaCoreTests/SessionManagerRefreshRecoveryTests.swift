import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
@testable import VouchaAuth
import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class SessionManagerRefreshRecoveryTests: XCTestCase {
    func testStartupServerFailurePreservesCookiesAndCanRecover() async throws {
        let fixture = try makeSession(responses: [
            .http(503, Data("{}".utf8)),
            .http(200, ApiFixtureLoader.data("swift.my.identity.default"))
        ])
        defer { SessionRefreshURLProtocol.unregister(host: fixture.host) }

        await fixture.manager.restoreSession()

        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["saved-session"])
        XCTAssertEqual(fixture.transport.requestPaths(), ["/api/v1/my/identity"])

        let recovered = await fixture.manager.refresh()

        XCTAssertTrue(recovered)
        XCTAssertTrue(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["saved-session"])
        XCTAssertEqual(fixture.transport.requestPaths().count, 2)
    }

    func testTimeoutPreservesIdentityAndUnauthorizedClearsRetryError() async throws {
        let fixture = try makeSession(responses: [.failure(.timedOut), .http(401, Data("{}".utf8))])
        defer { SessionRefreshURLProtocol.unregister(host: fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            RefreshIdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)

        let timedOut = await fixture.manager.refresh()

        XCTAssertFalse(timedOut)
        XCTAssertEqual(fixture.manager.currentUserId, identity.id)
        XCTAssertEqual(fixture.manager.uiLocale, identity.uiLocale)
        XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["saved-session"])

        let unauthorized = await fixture.manager.refresh()

        XCTAssertFalse(unauthorized)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.uiLocale)
        XCTAssertNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertEqual(fixture.transport.requestPaths().count, 2)
    }

    private func makeSession(responses: [SessionRefreshReply]) throws -> SessionRefreshSetup {
        let host = "refresh-\(UUID().uuidString.lowercased()).example.test"
        let transport = SessionRefreshTransport(responses: responses)
        SessionRefreshURLProtocol.register(transport, host: host)
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        try jar.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .domain: host, .path: "/", .name: "st", .value: "saved-session"
        ])))
        XCTAssertEqual(jar.cookies?.map(\.value), ["saved-session"])
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://\(host)"))),
            cookieStorage: jar,
            protocolClasses: [SessionRefreshURLProtocol.self],
            bootstrapSession: false,
            sessionConfiguration: configuration
        )
        return SessionRefreshSetup(
            manager: SessionManager(client: client, cookieStorage: jar),
            jar: jar, transport: transport, host: host
        )
    }
}

private struct SessionRefreshSetup {
    let manager: SessionManager
    let jar: HTTPCookieStorage
    let transport: SessionRefreshTransport
    let host: String
}

private struct RefreshIdentityEnvelope: Decodable {
    let identity: PrivateUser
}
