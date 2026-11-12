import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
@testable import VouchaAuth
import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class SessionRenewalReviewTests: XCTestCase {
    private struct Empty: Decodable {}
    private struct Identity: Decodable { let identity: PrivateUser }
    private let login = "/api/v1/auth/email-address/login"

    func testRenewalCompletingBeforeLoginDoesNotDiscardSessionEstablishingCookies() async throws {
        for path in [
            login,
            "/api/v1/auth/native/email-address/login",
            "/api/v1/auth/mfa/totp/verification",
            "/api/v1/auth/passkeys/authentication/verify",
            "/api/v1/auth/oauth/apple/continue",
            "/api/v1/auth/oauth/authorizations/flow/complete"
        ] {
            let fixture = try setup()
            let entered = expectation(description: "authentication pending")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
                entered.fulfill()
                return nil
            }
            let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: path)) }
            await fulfillment(of: [entered], timeout: 5)
            try await fixture.client.refreshSession()
            fixture.transport.release(loginResponse("signed-in"), path: path)
            _ = try await authentication.value
            XCTAssertEqual(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" }?.value, "signed-in")
            try await fixture.client.refreshSession()
            XCTAssertEqual(
                try JSONSerialization
                    .jsonObject(with: XCTUnwrap(fixture.transport.captured.last?.httpBody)) as? [String: String],
                ["dt": "d-renewed", "st": "signed-in"]
            )
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testFailedLoginAndNonEstablishingAuthResponsesCannotReplaceRenewedCookies() async throws {
        for (method, path, status) in [
            (HTTPMethod.POST, login, 401), (.GET, login, 200),
            (.POST, "/api/v1/auth/email-address/tokens", 200),
            (.POST, "/api/v1/auth/passkeys/authentication/options", 200),
            (.POST, "/api/v1/auth/re-auth/providers/github/proofs", 200),
            (.DELETE, "/api/v1/auth/oauth/github/connect", 200),
            (.POST, "/api/v1/auth/oauth/authorizations/flow/extra/complete", 200),
            (.POST, "/api/v1/auth/oauth/authorizations/flow/result", 200),
            (.POST, "/api/v1/auth/logout", 200)
        ] {
            let fixture = try setup()
            let entered = expectation(description: "response pending")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
                entered.fulfill()
                return nil
            }
            let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(method, path: path)) }
            await fulfillment(of: [entered], timeout: 5)
            try await fixture.client.refreshSession()
            var response = loginResponse("ineligible")
            response = .init(status: status, body: response.body, headers: response.headers)
            fixture.transport.release(response, path: path)
            if status == 401 {
                do { _ = try await pending.value
                    XCTFail("Expected original authentication failure")
                } catch VouchaError.unauthorized {}
            } else { _ = try await pending.value }
            XCTAssertEqual(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" }?.value, "s-renewed")
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testEligibleResponseWithoutSessionCookiesCannotBypassRevisionFence() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "authentication pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
            entered.fulfill()
            return nil
        }
        let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
        await fulfillment(of: [entered], timeout: 5)
        try await fixture.client.refreshSession()
        fixture.transport.release(
            .init(status: 200, body: Data("{}".utf8), headers: ["Set-Cookie": "preference=stale; Path=/"]),
            path: login
        )
        _ = try await pending.value
        XCTAssertNil(fixture.jar.cookies?.first { $0.name == "preference" })
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-renewed")
    }

    func testOlderOverlappingLoginCannotReplaceNewerCompletedAuthentication() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let olderPath = login
        let newerPath = "/api/v1/auth/native/email-address/login"
        let olderEntered = expectation(description: "older authentication pending")
        let newerEntered = expectation(description: "newer authentication pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
            if request.url!.path == olderPath { olderEntered.fulfill() } else { newerEntered.fulfill() }
            return nil
        }
        let older = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
        await fulfillment(of: [olderEntered], timeout: 5)
        let newer = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: newerPath)) }
        await fulfillment(of: [newerEntered], timeout: 5)
        try await fixture.client.refreshSession()
        fixture.transport.release(loginResponse("newer-account"), path: newerPath)
        _ = try await newer.value
        fixture.transport.release(loginResponse("older-account"), path: login)
        do { _ = try await older.value
            XCTFail("Completed account boundary must fence the older response")
        } catch is CancellationError {}
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "newer-account")
    }

    func testSignOutStillFencesEligiblePendingLoginAfterRenewal() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "authentication pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
            entered.fulfill()
            return nil
        }
        let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
        await fulfillment(of: [entered], timeout: 5)
        try await fixture.client.refreshSession()
        try await fixture.client.clearSessionCookiesChecked()
        fixture.transport.release(loginResponse("late-login"), path: login)
        do { _ = try await pending.value
            XCTFail("Sign-out epoch must remain authoritative")
        } catch is CancellationError {}
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testLifecycleMalformedEmptyAndNullRenewalsClearPreviouslyPublishedIdentity() async throws {
        for data in [Data(), Data("null".utf8), Data("{".utf8)] {
            let fixture = try setup()
            let manager = SessionManager(client: fixture.client)
            try manager.synchronize(with: APIClient.makeDecoder().decode(
                Identity.self,
                from: ApiFixtureLoader.data("swift.my.identity.default")
            ).identity)
            fixture.transport.handler = { _, _ in .init(status: 200, body: data) }
            let refreshed = await manager.refresh()
            XCTAssertFalse(refreshed)
            XCTAssertFalse(manager.isSignedIn)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
            XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/api/v1/session"])
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testAccessCookiePrunedAfterDispatchStillTriggersDeviceOnlyRenewal() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "protected request pending")
        fixture.transport.handler = { request, count in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
            if count == 1 { entered.fulfill()
                return nil
            }
            return SessionRecoveryTransport.empty
        }
        let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(path: "/protected")) }
        await fulfillment(of: [entered], timeout: 5)
        let revision = fixture.client.sessionRevision
        // Foundation removes expired access cookies before renewal; this models that pruned jar without a clock delay.
        for cookie in fixture.jar.cookies ?? [] where cookie.name == "st" {
            fixture.jar.deleteCookie(cookie)
        }
        XCTAssertEqual(fixture.client.sessionRevision, revision)
        fixture.transport.release(SessionRecoveryTransport.unauthorized, path: "/protected")
        _ = try await pending.value
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/protected", "/api/v1/session", "/protected"])
        XCTAssertEqual(
            try JSONSerialization
                .jsonObject(with: XCTUnwrap(fixture.transport.captured[1].httpBody)) as? [String: String],
            ["dt": "d-old"]
        )
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-renewed")
    }

    func testSharedTransportTimeoutDuringBodyReadReleasesRenewalForNextAttempt() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let session = await fixture.client.session
        XCTAssertEqual(session.configuration.timeoutIntervalForRequest, 17)
        XCTAssertEqual(session.configuration.timeoutIntervalForResource, 23)
        let entered = expectation(description: "renewal body pending")
        fixture.transport.handler = { _, _ in entered.fulfill()
            return nil
        }
        let pending = Task { try await fixture.client.refreshSession() }
        await fulfillment(of: [entered], timeout: 5)
        fixture.transport.deliverHeaders(path: "/api/v1/session")
        fixture.transport.fail(URLError(.timedOut), path: "/api/v1/session")
        do { try await pending.value
            XCTFail("Expected the transport timeout through the body read")
        } catch let error as URLError { XCTAssertEqual(
            error.code,
            .timedOut
        ) }
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-old")
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens("next") }
        try await fixture.client.refreshSession()
        XCTAssertEqual(fixture.transport.captured.count, 2)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-next")
    }

    private func loginResponse(_ token: String) -> SessionRecoveryTransport.Reply {
        .init(status: 200, body: Data("{}".utf8), headers: ["Set-Cookie": "st=\(token); Path=/; HttpOnly"])
    }

    private func setup() throws -> Fixture {
        let host = "review-\(UUID().uuidString.lowercased()).test"
        let url = URL(string: "http://\(host)")!
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 17
        configuration.timeoutIntervalForResource = 23
        configuration.protocolClasses = [SessionRecoveryProtocol.self]
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        for (name, value) in [("dt", "d-old"), ("st", "s-old")] {
            jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: name, .value: value])!)
        }
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let client = APIClient(
            config: AppConfig(baseURL: url),
            cookieStorage: jar,
            bootstrapSession: true,
            sessionConfiguration: configuration
        )
        return Fixture(client: client, jar: jar, url: url, host: host, transport: transport)
    }

    private struct Fixture {
        let client: APIClient
        let jar: HTTPCookieStorage
        let url: URL
        let host: String
        let transport: SessionRecoveryTransport
    }
}
