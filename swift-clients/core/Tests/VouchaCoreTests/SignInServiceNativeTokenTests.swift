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

    private func setup() -> (APIClient, SignInService, HTTPCookieStorage, String, SessionRecoveryTransport) {
        let host = "native-login-\(UUID().uuidString.lowercased()).test"
        let jar = URLSessionConfiguration.ephemeral.httpCookieStorage!
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://\(host)")!),
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: false
        )
        let service = SignInService(client: client, sessionManager: SessionManager(client: client))
        return (client, service, jar, host, transport)
    }

    func testBodyTokensAreInstalledBeforeSessionRefresh() async throws {
        let (_, service, jar, host, transport) = setup()
        defer { SessionRecoveryProtocol.remove(host) }
        let device = Self.jwt("device"), session = Self.jwt("session")
        transport.handler = { request, _ in
            switch request.url!.path {
            case "/api/v1/auth/native/email-address/login":
                return .init(status: 200, body: self.loginBody(dt: device, st: session))
            case "/api/v1/session": return SessionRecoveryTransport.tokens("fresh", uid: "user")
            default: return .init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default"))
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

    func testMFAResponseDoesNotInstallCredentials() async throws {
        let (_, service, jar, host, transport) = setup()
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
        let (_, service, jar, host, transport) = setup()
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
        let (client, _, jar, host, _) = setup()
        defer { SessionRecoveryProtocol.remove(host) }
        do {
            try await client.installNativeLoginTokens(
                deviceToken: Self.jwt("device", expiresIn: -10), sessionToken: Self.jwt("session")
            )
            XCTFail("An expired token must not be installed")
        } catch VouchaError.unauthorized {}
        XCTAssertTrue(jar.cookies?.isEmpty ?? true)
    }

    func testTokensAlreadyInstalledFromSetCookieAreKept() async throws {
        let (client, _, jar, host, _) = setup()
        defer { SessionRecoveryProtocol.remove(host) }
        for (name, value) in [("dt", "fixture-device-jwt"), ("st", "fixture-session-jwt")] {
            jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: name, .value: value])!)
        }
        try await client.installNativeLoginTokens(deviceToken: "fixture-device-jwt", sessionToken: "fixture-session-jwt")
        XCTAssertEqual(jar.cookies?.count, 2)
    }
}
