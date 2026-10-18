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

    func testSignedInStateWaitsForPersistenceAcknowledgementAndFailureStaysRetryable() async throws {
        let gate = RefreshPersistenceGate()
        let fixture = try makeSession(
            responses: [
                .http(200, ApiFixtureLoader.data("swift.my.identity.default")),
                .http(200, ApiFixtureLoader.data("swift.my.identity.default"))
            ],
            beforePublishSignedIn: { try await gate.waitForAcknowledgement() }
        )
        defer { SessionRefreshURLProtocol.unregister(host: fixture.host) }

        let firstRefresh = Task { await fixture.manager.refresh() }
        await gate.waitUntilEntered()
        XCTAssertFalse(fixture.manager.isSignedIn)
        await gate.reject()
        let firstRecovered = await firstRefresh.value
        XCTAssertFalse(firstRecovered)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)

        let secondRefresh = Task { await fixture.manager.refresh() }
        await gate.waitUntilEnteredAgain()
        XCTAssertFalse(fixture.manager.isSignedIn)
        await gate.acknowledge()
        let secondRecovered = await secondRefresh.value
        XCTAssertTrue(secondRecovered)
        XCTAssertTrue(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.lastRefreshErrorMessage)
    }

    func testPersistenceFailureRetainsSameOwnerButClearsDifferentOwner() async throws {
        let originalData = ApiFixtureLoader.data("swift.my.identity.default")
        let originalIdentity = try APIClient.makeDecoder().decode(
            RefreshIdentityEnvelope.self, from: originalData
        ).identity
        var differentEnvelope = try XCTUnwrap(
            JSONSerialization.jsonObject(with: originalData) as? [String: Any]
        )
        var differentIdentity = try XCTUnwrap(differentEnvelope["identity"] as? [String: Any])
        differentIdentity["id"] = "different-account-id"
        differentEnvelope["identity"] = differentIdentity
        let differentData = try JSONSerialization.data(withJSONObject: differentEnvelope)

        for (responseData, shouldRetainOwner) in [(originalData, true), (differentData, false)] {
            let fixture = try makeSession(
                responses: [.http(200, responseData)],
                beforePublishSignedIn: { throw URLError(.cannotWriteToFile) }
            )
            fixture.manager.synchronize(with: originalIdentity)

            let refreshed = await fixture.manager.refresh()

            XCTAssertFalse(refreshed)
            XCTAssertEqual(fixture.manager.currentUserId, shouldRetainOwner ? originalIdentity.id : nil)
            XCTAssertEqual(fixture.manager.uiLocale, shouldRetainOwner ? originalIdentity.uiLocale : nil)
            XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)
            XCTAssertEqual(fixture.transport.requestPaths(), ["/api/v1/my/identity"])
            SessionRefreshURLProtocol.unregister(host: fixture.host)
        }
    }

    private func makeSession(
        responses: [SessionRefreshReply],
        beforePublishSignedIn: (@Sendable () async throws -> Void)? = nil
    ) throws -> SessionRefreshSetup {
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
            manager: SessionManager(
                client: client, cookieStorage: jar, beforePublishSignedIn: beforePublishSignedIn
            ),
            jar: jar, transport: transport, host: host
        )
    }
}

private actor RefreshPersistenceGate {
    private var pending: CheckedContinuation<Void, Error>?
    private var calls = 0
    private var enteredWaiters: [CheckedContinuation<Void, Never>] = []

    func waitForAcknowledgement() async throws {
        calls += 1
        let waiters = enteredWaiters
        enteredWaiters.removeAll()
        waiters.forEach { $0.resume() }
        try await withCheckedThrowingContinuation { pending = $0 }
    }

    func waitUntilEntered() async {
        if calls > 0 { return }
        await withCheckedContinuation { enteredWaiters.append($0) }
    }

    func waitUntilEnteredAgain() async {
        if calls > 1 { return }
        await withCheckedContinuation { enteredWaiters.append($0) }
    }

    func reject() {
        pending?.resume(throwing: URLError(.cannotWriteToFile))
        pending = nil
    }

    func acknowledge() {
        pending?.resume()
        pending = nil
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
