import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class SessionRenewalCleanupAuthenticationTests: XCTestCase {
    private struct Empty: Decodable {}
    private let login = "/api/v1/auth/email-address/login"
    private let newerLogin = "/api/v1/auth/native/email-address/login"
    private static let failures: [SessionRecoveryTransport.Reply] = [
        .init(status: 200, body: Data("{".utf8)),
        .init(status: 200, body: Data("{}".utf8)),
        .init(status: 401, body: Data(#"{"code":"SESSION_REFRESH_REUSED"}"#.utf8)),
        .init(status: 401, body: Data(#"{"code":"SESSION_RELOGIN_REQUIRED"}"#.utf8)),
        SessionRecoveryTransport.tokens("anonymous", uid: nil)
    ]

    func testLatestLoginDispatchedBeforeUnusableRenewalCanEstablishReplacementSession() async throws {
        for failure in Self.failures {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            let entered = expectation(description: "login already pending")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return failure }
                entered.fulfill()
                return nil
            }
            let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
            await fulfillment(of: [entered], timeout: 5)
            try await assertRenewalClears(fixture)
            fixture.transport.release(loginResponse("authenticated"), path: login)
            do { _ = try await authentication.value } catch {
                XCTFail("Latest authentication must survive unusable renewal: \(error)")
                continue
            }
            XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["authenticated"])
            XCTAssertEqual(fixture.persisted.snapshots.last?["st"], "authenticated")
            fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens("next") }
            try await fixture.client.refreshSession()
            XCTAssertEqual(
                try JSONDecoder().decode(
                    [String: String].self,
                    from: XCTUnwrap(fixture.transport.captured.last?.httpBody)
                ),
                ["st": "authenticated"]
            )
        }
    }

    func testExplicitLogoutAfterAutomaticCleanupStillFencesPendingAuthentication() async throws {
        for failure in Self.failures {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            let entered = expectation(description: "login pending before logout")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return failure }
                entered.fulfill()
                return nil
            }
            let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
            await fulfillment(of: [entered], timeout: 5)
            try await assertRenewalClears(fixture)
            try await fixture.client.clearSessionCookiesChecked()
            fixture.transport.release(loginResponse("after-logout"), path: login)
            await assertCanceled(authentication)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        }
    }

    func testAutomaticCleanupPreservesOnlyNewestAuthenticationInBothCompletionOrders() async throws {
        for olderFirst in [true, false] {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            let olderEntered = expectation(description: "older login pending")
            let newerEntered = expectation(description: "newer login pending")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return Self.failures[0] }
                if request.url!.path == self.login { olderEntered.fulfill() } else { newerEntered.fulfill() }
                return nil
            }
            let older = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
            await fulfillment(of: [olderEntered], timeout: 5)
            let newer = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: newerLogin)) }
            await fulfillment(of: [newerEntered], timeout: 5)
            try await assertRenewalClears(fixture)
            if olderFirst {
                fixture.transport.release(loginResponse("older"), path: login)
                await assertCanceled(older)
            }
            fixture.transport.release(loginResponse("newer"), path: newerLogin)
            _ = try await newer.value
            if !olderFirst {
                fixture.transport.release(loginResponse("older"), path: login)
                await assertCanceled(older)
            }
            XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["newer"])
        }
    }

    func testOrdinaryTerminalCleanupPreservesIndependentPendingAuthentication() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "login pending before terminal feature response")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/feature" { return Self.failures[2] }
            entered.fulfill()
            return nil
        }
        let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
        await fulfillment(of: [entered], timeout: 5)
        do { let _: Empty = try await fixture.client.send(Endpoint(path: "/feature"))
            XCTFail("Expected terminal failure")
        } catch VouchaError.unauthorized {}
        fixture.transport.release(loginResponse("authenticated"), path: login)
        _ = try await authentication.value
        XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["authenticated"])
    }

    func testRotatedPersistenceFailurePreservesIndependentLoginWhenItsLaterWriteSucceeds() async throws {
        let fixture = try setup(failRotatedWrite: true)
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "login pending before rotated write fails")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            entered.fulfill()
            return nil
        }
        let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
        await fulfillment(of: [entered], timeout: 5)
        do { try await fixture.client.refreshSession()
            XCTFail("Expected rotated write failure")
        } catch { XCTAssertEqual((error as NSError).code, CocoaError.fileWriteNoPermission.rawValue) }
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        XCTAssertEqual(fixture.persisted.snapshots.last, [:])
        fixture.transport.release(loginResponse("authenticated"), path: login)
        _ = try await authentication.value
        XCTAssertEqual(fixture.jar.cookies?.map(\.value), ["authenticated"])
        XCTAssertEqual(fixture.persisted.snapshots.last?["st"], "authenticated")
    }

    func testFailedNewerAuthenticationStillFencesOlderResponsesWithAndWithoutCookies() async throws {
        for hasCookies in [false, true] {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            let olderEntered = expectation(description: "older authentication pending")
            let newerEntered = expectation(description: "newer authentication pending")
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return Self.failures[0] }
                if request.url!.path == self.login { olderEntered.fulfill() } else { newerEntered.fulfill() }
                return nil
            }
            let older = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
            await fulfillment(of: [olderEntered], timeout: 5)
            let newer = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: newerLogin)) }
            await fulfillment(of: [newerEntered], timeout: 5)
            try await assertRenewalClears(fixture)
            fixture.transport.release(.init(status: 403, body: Data("{}".utf8)), path: newerLogin)
            do { _ = try await newer.value
                XCTFail("Expected newer authentication failure")
            } catch VouchaError.forbidden {}
            fixture.transport.release(hasCookies ? loginResponse("older") : SessionRecoveryTransport.empty, path: login)
            await assertCanceled(older)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        }
    }

    func testFailedAuthenticationCannotRestoreAnyCookiesAfterAutomaticCleanup() async throws {
        for failedPersistence in [false, true] {
            for cookie in ["st=old-session; Path=/; HttpOnly", "preference=stale; Path=/"] {
                let fixture = try setup(failRotatedWrite: failedPersistence)
                defer { SessionRecoveryProtocol.remove(fixture.host) }
                let entered = expectation(description: "failed authentication pending across cleanup")
                fixture.transport.handler = { request, _ in
                    if request.url!.path == "/api/v1/session" {
                        return failedPersistence ? SessionRecoveryTransport.tokens() : Self.failures[0]
                    }
                    entered.fulfill()
                    return nil
                }
                let authentication = Task { () -> Empty in try await fixture.client.send(Endpoint(.POST, path: login)) }
                await fulfillment(of: [entered], timeout: 5)
                if failedPersistence {
                    do { try await fixture.client.refreshSession()
                        XCTFail("Expected rotated write failure")
                    } catch { XCTAssertEqual((error as NSError).code, CocoaError.fileWriteNoPermission.rawValue) }
                } else {
                    try await assertRenewalClears(fixture)
                }
                fixture.transport.release(.init(
                    status: 403, body: Data(#"{"code":"LOGIN_FAILED"}"#.utf8), headers: ["Set-Cookie": cookie]
                ), path: login)
                do { _ = try await authentication.value
                    XCTFail("Expected the original failed login response")
                } catch let VouchaError.forbidden(code) { XCTAssertEqual(code, "LOGIN_FAILED") }
                XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
                XCTAssertEqual(fixture.persisted.snapshots.last, [:])
            }
        }
    }

    func testAutomaticCleanupStillRejectsStaleOrdinaryResponseCookies() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "ordinary response pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return Self.failures[0] }
            entered.fulfill()
            return nil
        }
        let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(path: "/ordinary")) }
        await fulfillment(of: [entered], timeout: 5)
        try await assertRenewalClears(fixture)
        fixture.transport.release(loginResponse("stale"), path: "/ordinary")
        await assertCanceled(pending)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    private func assertRenewalClears(_ fixture: Fixture) async throws {
        let epoch = fixture.client.sessionEpoch
        do { try await fixture.client.refreshSession()
            XCTFail("Expected unusable renewal")
        } catch VouchaError.unauthorized {}
        XCTAssertGreaterThan(fixture.client.sessionEpoch, epoch)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        XCTAssertEqual(fixture.persisted.snapshots.last, [:])
    }

    private func assertCanceled(_ task: Task<Empty, Error>) async {
        do { _ = try await task.value
            XCTFail("A stale request must remain fenced")
        } catch is CancellationError {} catch { XCTFail("Unexpected stale-request failure: \(error)") }
    }

    private func loginResponse(_ token: String) -> SessionRecoveryTransport.Reply {
        .init(status: 200, body: Data("{}".utf8), headers: ["Set-Cookie": "st=\(token); Path=/; HttpOnly"])
    }

    private func setup(failRotatedWrite: Bool = false) throws -> Fixture {
        let host = "cleanup-auth-\(UUID().uuidString.lowercased()).test"
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        for (name, value) in [("dt", "old-device"), ("st", "old-session")] {
            try jar.setCookie(XCTUnwrap(HTTPCookie(properties: [
                .domain: host,
                .path: "/",
                .name: name,
                .value: value
            ])))
        }
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let persisted = CookiePersistenceRecorder(jar: jar)
        let failure = ThrowOnce()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://\(host)"))), cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self], bootstrapSession: true,
            sessionConfiguration: configuration, persistCookiesChecked: {
                if failRotatedWrite, jar.cookies?.first(where: { $0.name == "st" })?.value == "s-fresh",
                   failure.consume() { throw CocoaError(.fileWriteNoPermission) }
                persisted.snapshot()
            }
        )
        return Fixture(client: client, jar: jar, host: host, transport: transport, persisted: persisted)
    }

    private struct Fixture {
        let client: APIClient
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
        let persisted: CookiePersistenceRecorder
    }
}

private final class ThrowOnce: @unchecked Sendable {
    private let lock = NSLock()
    private var available = true
    func consume() -> Bool {
        lock.withLock {
            guard available else { return false }
            available = false
            return true
        }
    }
}
