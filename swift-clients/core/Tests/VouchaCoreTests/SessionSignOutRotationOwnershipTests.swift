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
final class SessionSignOutRotationOwnershipTests: XCTestCase {
    func testEveryPreSignOutHookAndCompletionOrderRetainsOrDiscardsHeldRotationAtCommit() async throws {
        for hook in [Hook.deviceCleanup, .durableCleanup] {
            for fails in [false, true] {
                for rotationFirst in [false, true] {
                    try await exercise(hook: hook, fails: fails, rotationFirst: rotationFirst)
                }
            }
        }
    }

    private func exercise(hook: Hook, fails: Bool, rotationFirst: Bool) async throws {
        let gate = SignOutHookGate()
        let beforeClear: (@Sendable () async throws -> Void)? = if hook == .durableCleanup { { try await gate.wait() } }
        else { nil }
        let fixture = try makeFixture(beforeClear: beforeClear)
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let data = ApiFixtureLoader.data("swift.my.identity.default")
        struct Identity: Decodable { let identity: PrivateUser }
        let user = try APIClient.makeDecoder().decode(Identity.self, from: data).identity
        fixture.manager.synchronize(with: user)
        if hook == .deviceCleanup { fixture.manager.setBeforeSignOut { _ in try await gate.wait() } }
        let entered = expectation(description: "PATCH held for \(hook) fails=\(fails) rotationFirst=\(rotationFirst)")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { entered.fulfill()
                return nil
            }
            return SessionRecoveryTransport.empty
        }
        let rotation = Task { try await fixture.client.refreshSession() }
        await fulfillment(of: [entered], timeout: 5)
        let signOut = Task { await fixture.manager.signOut() }
        await gate.waitUntilEntered()
        if rotationFirst {
            fixture.transport.release(SessionRecoveryTransport.tokens(), path: "/api/v1/session")
            _ = await rotation.result
            XCTAssertEqual(
                fixture.jar.cookies?.first { $0.name == "st" }?.value,
                "s-fresh",
                "A preparation hook cannot discard a server-consumed PATCH"
            )
        }
        await gate.finish(fails: fails)
        await signOut.value
        if !rotationFirst {
            fixture.transport.release(SessionRecoveryTransport.tokens(), path: "/api/v1/session")
            _ = await rotation.result
        }
        if fails {
            XCTAssertTrue(fixture.manager.isSignedIn)
            XCTAssertNotNil(fixture.manager.lastSignOutErrorMessage)
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-fresh")
            XCTAssertEqual(fixture.persisted.snapshots.last?["st"], "s-fresh")
            XCTAssertFalse(fixture.transport.captured.contains { $0.url!.path == "/api/v1/auth/logout" })
            fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens("next") }
            try await fixture.client.refreshSession()
            XCTAssertEqual(
                try JSONDecoder().decode(
                    [String: String].self,
                    from: XCTUnwrap(fixture.transport.captured.last?.httpBody)
                ),
                ["dt": "d-fresh", "st": "s-fresh"]
            )
        } else {
            XCTAssertFalse(fixture.manager.isSignedIn)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
            XCTAssertEqual(fixture.persisted.snapshots.last, [:])
            let logout = try XCTUnwrap(fixture.transport.captured.first { $0.url!.path == "/api/v1/auth/logout" })
            XCTAssertTrue(logout.value(forHTTPHeaderField: "Cookie")?
                .contains(rotationFirst ? "s-fresh" : "s-original") == true)
        }
    }

    private func makeFixture(beforeClear: (@Sendable () async throws -> Void)?) throws -> Fixture {
        let host = "signout-ownership-\(UUID().uuidString.lowercased()).test"
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        for (name, value) in [("dt", "d-original"), ("st", "s-original")] {
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
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://\(host)"))), cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self], bootstrapSession: true,
            sessionConfiguration: configuration, persistCookies: { persisted.snapshot() }
        )
        return Fixture(
            client: client, manager: SessionManager(client: client, beforeClearSessionCookies: beforeClear),
            jar: jar, host: host, transport: transport, persisted: persisted
        )
    }

    private enum Hook { case deviceCleanup, durableCleanup }
    private struct Fixture {
        let client: APIClient
        let manager: SessionManager
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
        let persisted: CookiePersistenceRecorder
    }
}

private actor SignOutHookGate {
    private var entered = false
    private var entry: CheckedContinuation<Void, Never>?
    private var completion: CheckedContinuation<Void, Error>?
    func wait() async throws {
        entered = true
        entry?.resume()
        entry = nil
        try await withCheckedThrowingContinuation { completion = $0 }
    }

    func waitUntilEntered() async {
        if entered { return }
        await withCheckedContinuation { entry = $0 }
    }

    func finish(fails: Bool) {
        if fails { completion?.resume(throwing: CocoaError(.fileWriteNoPermission)) }
        else { completion?.resume() }
        completion = nil
    }
}
