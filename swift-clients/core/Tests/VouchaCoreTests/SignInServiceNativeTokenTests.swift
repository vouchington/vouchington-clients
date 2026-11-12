import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
@testable import VouchaAuth
import VouchaCore
import XCTest

@MainActor
final class SignInServiceNativeTokenTests: XCTestCase {
    private static func jwt(_ name: String, expiresIn: TimeInterval = 3_600) -> String {
        let claims = ["exp": Int(Date().timeIntervalSince1970 + expiresIn)]
        let payload = (try? JSONSerialization.data(withJSONObject: claims)) ?? Data()
        return "h.\(payload.base64URLEncodedString()).\(name)"
    }

    private func loginBody(dt: String, st: String) -> Data {
        let json = #"{"did":"d","sid":"s","uid":"u","dt":{"token":"\#(dt)","payload":{"did":"d"}},"#
            + #""st":{"token":"\#(st)","payload":{"did":"d","sid":"s","uid":"u"}}}"#
        return Data(json.utf8)
    }

    private func refreshTokens(
        storing snapshot: CookieSnapshot, from jar: HTTPCookieStorage
    ) -> SessionRecoveryTransport.Reply {
        snapshot.store(jar.cookies ?? [])
        return SessionRecoveryTransport.tokens("fresh", uid: "user")
    }

    private final class CookieSnapshot: @unchecked Sendable {
        private let lock = NSLock()
        private var stored: [HTTPCookie] = []
        func store(_ cookies: [HTTPCookie]) {
            lock.lock()
            stored = cookies
            lock.unlock()
        }

        var cookies: [HTTPCookie] {
            lock.lock()
            defer { lock.unlock() }
            return stored
        }
    }

    private func setup(scheme: String = "http")
        throws -> (APIClient, SignInService, HTTPCookieStorage, String, SessionRecoveryTransport) {
        let host = "native-login-\(UUID().uuidString.lowercased()).test"
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "\(scheme)://\(host)")!),
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: false
        )
        let service = SignInService(client: client, sessionManager: SessionManager(client: client))
        return (client, service, jar, host, transport)
    }

    func testBodyTokensAreInstalledBeforeSessionRefresh() async throws {
        let (_, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        let device = Self.jwt("device"), session = Self.jwt("session")
        transport.handler = { request, _ in
            switch request.url!.path {
            case "/api/v1/auth/native/email-address/login":
                .init(status: 200, body: self.loginBody(dt: device, st: session))
            case "/api/v1/session": SessionRecoveryTransport.tokens("fresh", uid: "user")
            default: .init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default"))
            }
        }
        let challenge = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")
        XCTAssertNil(challenge)
        let refresh = try XCTUnwrap(transport.captured.first { $0.url!.path == "/api/v1/session" })
        let cookieHeader = try XCTUnwrap(refresh.value(forHTTPHeaderField: "Cookie"))
        XCTAssertTrue(cookieHeader.contains("dt=\(device)"))
        XCTAssertTrue(cookieHeader.contains("st=\(session)"))
        XCTAssertNotNil(jar.cookies?.first { $0.name == "st" }?.expiresDate)
    }

    func testReplacingAWeakerExistingCookieStillSetsSecureAndHttpOnly() async throws {
        let (_, service, jar, host, transport) = try setup(scheme: "https")
        defer { SessionRecoveryProtocol.remove(host) }
        for name in ["dt", "st"] {
            let weak = try XCTUnwrap(HTTPCookie(properties: [
                .name: name, .value: "old-\(name)", .domain: host, .path: "/"
            ]))
            jar.setCookie(weak)
        }
        let device = Self.jwt("device"), session = Self.jwt("session")
        let atRefresh = CookieSnapshot()
        transport.handler = { request, _ in
            switch request.url!.path {
            case "/api/v1/auth/native/email-address/login":
                .init(status: 200, body: self.loginBody(dt: device, st: session))
            case "/api/v1/session":
                self.refreshTokens(storing: atRefresh, from: jar)
            default: .init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default"))
            }
        }

        _ = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")

        for (name, value) in [("dt", device), ("st", session)] {
            let installed = try XCTUnwrap(atRefresh.cookies.first { $0.name == name && $0.value == value })
            XCTAssertTrue(installed.isSecure, "\(name) must be Secure")
            XCTAssertTrue(installed.isHTTPOnly, "\(name) must be HttpOnly")
        }
    }

    func testMFAResponseDoesNotInstallCredentials() async throws {
        let (_, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        transport.handler = { _, _ in
            .init(status: 200, body: Data(#"{"mfa_required":true,"login_attempt_id":"attempt-1"}"#.utf8))
        }
        let challenge = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")
        XCTAssertEqual(challenge?.loginAttemptId, "attempt-1")
        XCTAssertTrue(jar.cookies?.isEmpty ?? true)
        XCTAssertEqual(transport.captured.map { $0.url!.path }, ["/api/v1/auth/native/email-address/login"])
    }

    func testUnparseableBodyTokenFailsClosedWithoutInstallingAnything() async throws {
        let (_, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        transport.handler = { _, _ in
            .init(status: 200, body: self.loginBody(dt: Self.jwt("device"), st: "not-a-jwt"))
        }
        do {
            _ = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")
            XCTFail("A body token without a signed expiry must not sign the user in")
        } catch VouchaError.unauthorized {}
        XCTAssertTrue(jar.cookies?.isEmpty ?? true)
    }

    func testExpiredBodyTokenIsRejected() async throws {
        let (_, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        transport.handler = { _, _ in
            .init(status: 200, body: self.loginBody(dt: Self.jwt("device", expiresIn: -10), st: Self.jwt("session")))
        }
        do {
            _ = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")
            XCTFail("An expired token must not be installed")
        } catch VouchaError.unauthorized {}
        XCTAssertTrue(jar.cookies?.isEmpty ?? true)
    }

    func testTokensAlreadyInstalledFromSetCookieAreKept() async throws {
        let (_, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        let cookies = "dt=fixture-device-jwt; Path=/; HttpOnly, st=fixture-session-jwt; Path=/; HttpOnly"
        transport.handler = { request, _ in
            switch request.url?.path {
            case "/api/v1/auth/native/email-address/login":
                .init(
                    status: 200,
                    body: self.loginBody(dt: "fixture-device-jwt", st: "fixture-session-jwt"),
                    headers: ["Set-Cookie": cookies]
                )
            case "/api/v1/session": SessionRecoveryTransport.tokens("fresh", uid: "user")
            default: .init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default"))
            }
        }
        let challenge = try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23")
        XCTAssertNil(challenge)
        let refresh = try XCTUnwrap(transport.captured.first { $0.url?.path == "/api/v1/session" })
        XCTAssertTrue(refresh.value(forHTTPHeaderField: "Cookie")?.contains("dt=fixture-device-jwt") == true)
        XCTAssertNotNil(jar.cookies)
    }

    func testSignOutThatSupersedesTheLoginRejectsItsBodyTokens() async throws {
        let (client, service, jar, host, transport) = try setup()
        defer { SessionRecoveryProtocol.remove(host) }
        let stale = Self.jwt("stale-device")
        transport.handler = { request, _ in
            request.url?.path == "/api/v1/auth/native/email-address/login" ? nil : .init(status: 500, body: Data())
        }
        let login = Task { try await service.verifyEmailOTP(email: "a@example.test", code: "ABCDEFGH23") }
        while transport.captured.isEmpty {
            await Task.yield()
        }
        client.fenceSessionRequests()
        transport.release(.init(status: 200, body: loginBody(dt: stale, st: Self.jwt("stale-session"))))

        let result = await login.result
        if case .success = result { XCTFail("A superseded login must not sign the user in") }
        XCTAssertNil(jar.cookies?.first { $0.value == stale })
        XCTAssertFalse(transport.captured
            .contains {
                $0.url?.path == "/api/v1/session" && $0.value(forHTTPHeaderField: "Cookie")?.contains(stale) == true
            })
    }
}
