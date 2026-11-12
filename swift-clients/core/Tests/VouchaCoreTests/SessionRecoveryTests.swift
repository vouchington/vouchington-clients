import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaAuth
import VouchaCore
import XCTest

final class SessionRecoveryTests: XCTestCase {
    private struct Empty: Decodable {}

    func testAuthRequiredRotatesPairAndReplaysJSONOnce() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { request, count in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            return count == 1 ? SessionRecoveryTransport.unauthorized : SessionRecoveryTransport.empty
        }
        let _: Empty = try await fixture.client.send(Endpoint(.POST, path: "/work", body: ["text": "same body"]))
        let requests = fixture.transport.captured
        XCTAssertEqual(requests.map { $0.url!.path }, ["/work", "/api/v1/session", "/work"])
        let original = try XCTUnwrap(requests.first)
        let replay = try XCTUnwrap(requests.last)
        XCTAssertEqual(
            try JSONSerialization.jsonObject(with: XCTUnwrap(original.httpBody)) as? [String: String],
            ["text": "same body"]
        )
        XCTAssertEqual(original.httpBody, replay.httpBody)
        XCTAssertTrue(try XCTUnwrap(original.value(forHTTPHeaderField: "Cookie")).contains("s-old"))
        XCTAssertTrue(try XCTUnwrap(replay.value(forHTTPHeaderField: "Cookie")).contains("s-fresh"))
        let stored = try XCTUnwrap(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" })
        XCTAssertEqual(try XCTUnwrap(stored.expiresDate?.timeIntervalSinceNow), 900, accuracy: 5)
        XCTAssertTrue(stored.isHTTPOnly)
    }

    func testSecondUnauthorizedIsNotReplayedAndOtherCodesNeverRotate() async throws {
        for code in ["AUTH_REQUIRED", "OTHER_CODE"] {
            let fixture = try setup()
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
                return .init(status: 401, body: Data("{\"code\":\"\(code)\"}".utf8))
            }
            do {
                let _: Empty = try await fixture.client.send(Endpoint(path: "/work"))
                XCTFail("Expected unauthorized")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.count, code == "AUTH_REQUIRED" ? 3 : 1)
            XCTAssertEqual(fixture.jar.cookies?.count, code == "AUTH_REQUIRED" ? 0 : 2)
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testTwoClientsShareForcedRotationAndCanceledWaiterDoesNotCancelIt() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "rotation entered")
        fixture.transport.handler = { _, _ in entered.fulfill()
            return nil
        }
        let first = Task { try await fixture.client.refreshSession() }
        await fulfillment(of: [entered], timeout: 5)
        let other = try client(jar: fixture.jar, url: fixture.url)
        let firstCoordinator = await fixture.client.cookieCoordinator
        let secondCoordinator = await other.cookieCoordinator
        XCTAssertTrue(firstCoordinator === secondCoordinator)
        first.cancel()
        fixture.transport.release(SessionRecoveryTransport.tokens())
        do { try await first.value
            XCTFail("Expected canceled waiter")
        } catch is CancellationError {}
        XCTAssertEqual(fixture.transport.captured.count, 1)
        XCTAssertEqual(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" }?.value, "s-fresh")
    }

    func testLateResponseCannotRestoreCookiesAfterClear() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "request entered")
        fixture.transport.handler = { _, _ in entered.fulfill()
            return nil
        }
        let pending = Task { () -> Empty in try await fixture.client.send(Endpoint(path: "/work")) }
        await fulfillment(of: [entered], timeout: 5)
        await fixture.client.clearSessionCookies()
        fixture.transport.release(.init(
            status: 200,
            body: Data("{}".utf8),
            headers: ["Set-Cookie": "st=late; Path=/; HttpOnly"]
        ))
        do { _ = try await pending.value
            XCTFail("Expected stale response rejection")
        } catch is CancellationError {}
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testTerminalRefreshFailuresClearAndTransientFailuresPreservePair() async throws {
        for code in [
            "SESSION_RELOGIN_REQUIRED",
            "SESSION_REFRESH_REUSED",
            "SESSION_USER_AGENT_MISMATCH",
            "SESSION_JA4_MISMATCH",
            "TRANSIENT"
        ] {
            let fixture = try setup()
            fixture.transport.handler = { _, _ in .init(
                status: code == "TRANSIENT" ? 503 : 401,
                body: Data("{\"code\":\"\(code)\"}".utf8)
            ) }
            do { try await fixture.client.refreshSession()
                XCTFail("Expected failure")
            } catch {}
            XCTAssertEqual(fixture.jar.cookies?.count, code == "TRANSIENT" ? 2 : 0)
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testCheckedPersistenceFailurePreventsReplay() async throws {
        let fixture = try setup(persist: { throw CocoaError(.fileWriteNoPermission) })
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport
            .handler = { request, _ in
                request.url!.path == "/api/v1/session" ? SessionRecoveryTransport.tokens() : SessionRecoveryTransport
                    .unauthorized
            }
        do {
            let _: Empty = try await fixture.client.send(Endpoint(path: "/work"))
            XCTFail("Expected persistence failure")
        } catch let error as CocoaError { XCTAssertEqual(error.code, .fileWriteNoPermission) }
        XCTAssertEqual(fixture.transport.captured.count, 2)
    }

    func testReplayRegeneratesSignerOwnedAssertionsAndNestedErrorRecovers() async throws {
        let signer = RecoverySigner()
        let fixture = try setup(signer: signer)
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { request, count in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            return count == 1 ? .init(status: 401, body: Data("{\"error\":{\"code\":\"AUTH_REQUIRED\"}}".utf8)) :
                SessionRecoveryTransport.empty
        }
        let _: Empty = try await fixture.client.send(Endpoint(path: "/work"))
        XCTAssertEqual(
            fixture.transport.captured.map { $0.value(forHTTPHeaderField: RequestSignatureHeader.assertion) },
            ["assertion-1", "assertion-2", "assertion-3"]
        )
    }

    func testExplicitSingleUseCeremonyHeadersAreNotReplayed() async throws {
        for header in [RequestSignatureHeader.challengeId, RequestSignatureHeader.assertion] {
            let fixture = try setup()
            fixture.transport.handler = { _, _ in SessionRecoveryTransport.unauthorized }
            do {
                let _: Empty = try await fixture.client.send(Endpoint(
                    .POST,
                    path: "/ceremony",
                    headers: [header: "single-use"]
                ))
                XCTFail("Caller must handle the failed ceremony")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.count, 1)
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testExpiredAccessCookieCanRenewUsingUnexpiredDeviceCookie() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        for cookie in fixture.jar.cookies ?? [] where cookie.name == "st" {
            fixture.jar.deleteCookie(cookie)
        }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens() }
        try await fixture.client.refreshSession()
        let request = try XCTUnwrap(fixture.transport.captured.first)
        XCTAssertEqual(request.value(forHTTPHeaderField: "Cookie"), "dt=d-old")
        XCTAssertEqual(
            try JSONSerialization.jsonObject(with: XCTUnwrap(request.httpBody)) as? [String: String],
            ["dt": "d-old"]
        )
        XCTAssertEqual(
            fixture.jar.cookies(for: fixture.url)?.first { $0.name == "dt" }?.expiresDate?.timeIntervalSinceNow ?? 0,
            3_600,
            accuracy: 5
        )
        XCTAssertEqual(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" }?.value, "s-fresh")
    }

    func testDirectRefreshPreservesNativeTokenBodyAndHeadersAndRejectsConsumedPair() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens() }
        let endpoint = Endpoint.refreshSession(deviceToken: "d-old", sessionToken: "s-old")
            .withHeaders(["x-explicit-refresh": "kept"])
        let _: Empty = try await fixture.client.send(endpoint)
        let request = try XCTUnwrap(fixture.transport.captured.first)
        XCTAssertEqual(
            try JSONSerialization.jsonObject(with: XCTUnwrap(request.httpBody)) as? [String: String],
            ["dt": "d-old", "st": "s-old"]
        )
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-explicit-refresh"), "kept")
        do {
            let _: Empty = try await fixture.client.send(endpoint)
            XCTFail("Consumed credentials must not be sent again")
        } catch VouchaError.unauthorized {}
        XCTAssertEqual(fixture.transport.captured.count, 1)
    }

    func testForcedRefreshIgnoresAutomaticBootstrapOptOut() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens() }
        let client = APIClient(
            config: AppConfig(baseURL: fixture.url),
            cookieStorage: fixture.jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: false
        )
        try await client.refreshSession()
        let request = try XCTUnwrap(fixture.transport.captured.first)
        XCTAssertEqual(
            try JSONSerialization.jsonObject(with: XCTUnwrap(request.httpBody)) as? [String: String],
            ["dt": "d-old", "st": "s-old"]
        )
    }

    func testMalformedOrAnonymousRenewalClearsConsumedCredentialsWithoutReplay() async throws {
        var invalidEnvelope = try XCTUnwrap(JSONSerialization
            .jsonObject(with: SessionRecoveryTransport.tokens().body) as? [String: Any])
        var invalidSession = try XCTUnwrap(invalidEnvelope["session"] as? [String: Any])
        invalidSession["dt"] = ""
        invalidEnvelope["session"] = invalidSession
        let invalidTokens = try SessionRecoveryTransport.Reply(
            status: 200,
            body: JSONSerialization.data(withJSONObject: invalidEnvelope)
        )
        for reply in [SessionRecoveryTransport.empty, SessionRecoveryTransport.tokens(uid: nil), invalidTokens] {
            let fixture = try setup()
            fixture.transport
                .handler = { request, _ in
                    request.url!.path == "/api/v1/session" ? reply : SessionRecoveryTransport.unauthorized
                }
            do {
                let _: Empty = try await fixture.client.send(Endpoint(path: "/work"))
                XCTFail("Invalid authenticated renewal must require sign-in")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/work", "/api/v1/session"])
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testDirectNestedReloginFailureClearsWithoutAttemptingRotation() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in .init(
            status: 401,
            body: Data("{\"error\":{\"code\":\"SESSION_RELOGIN_REQUIRED\"}}".utf8)
        ) }
        do {
            let _: Empty = try await fixture.client.send(Endpoint(path: "/work"))
            XCTFail("Expected sign-in requirement")
        } catch VouchaError.unauthorized {}
        XCTAssertEqual(fixture.transport.captured.count, 1)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testLateTerminalFailureCannotClearCookiesFromNewAuthentication() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "old account request pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/old-account" { entered.fulfill()
                return nil
            }
            return .init(
                status: 200,
                body: Data("{}".utf8),
                headers: ["Set-Cookie": "st=new-account; Path=/; HttpOnly"]
            )
        }
        let old = Task { () -> Empty in try await fixture.client.send(Endpoint(path: "/old-account")) }
        await fulfillment(of: [entered], timeout: 5)
        let _: Empty = try await fixture.client.send(Endpoint(.POST, path: "/new-authentication"))
        fixture.transport.release(.init(status: 401, body: Data("{\"code\":\"SESSION_RELOGIN_REQUIRED\"}".utf8)))
        do { _ = try await old.value
            XCTFail("Old account response must be fenced")
        } catch is CancellationError {}
        XCTAssertEqual(fixture.jar.cookies(for: fixture.url)?.first { $0.name == "st" }?.value, "new-account")
    }

    func testAnonymousBootstrapIsValidForPublicRequests() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        for cookie in fixture.jar.cookies ?? [] {
            fixture.jar.deleteCookie(cookie)
        }
        fixture.transport.handler = { request, _ in
            request.url!.path == "/api/v1/session" ? SessionRecoveryTransport
                .tokens(uid: nil) : SessionRecoveryTransport.empty
        }
        let _: Empty = try await fixture.client.send(Endpoint(path: "/public"))
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/api/v1/session", "/public"])
        XCTAssertEqual(fixture.jar.cookies?.count, 2)
    }

    func testAuthenticationCeremonyBodiesAreNeverAutomaticallyReplayed() async throws {
        for path in [
            "/api/v1/auth/email-address/login",
            "/api/v1/auth/mfa/totp/verification",
            "/api/v1/auth/passkeys/authentication/verification"
        ] {
            let fixture = try setup()
            fixture.transport.handler = { _, _ in SessionRecoveryTransport.unauthorized }
            do {
                let _: Empty = try await fixture.client.send(Endpoint(
                    .POST,
                    path: path,
                    body: ["code": "single-use-proof"]
                ))
                XCTFail("Authentication failure must reach the ceremony owner")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.count, 1)
            XCTAssertEqual(fixture.transport.captured.first?.url?.path, path)
            SessionRecoveryProtocol.remove(fixture.host)
        }
    }

    func testProvidedUserAgentSurvivesRotationAndReplayUnchanged() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let configuration = URLSessionConfiguration.ephemeral
        configuration.httpCookieStorage = fixture.jar
        configuration.protocolClasses = [SessionRecoveryProtocol.self]
        configuration.httpAdditionalHeaders = ["User-Agent": "ProvidedNative/1.2 (existing client)"]
        let client = APIClient(
            config: AppConfig(baseURL: fixture.url),
            cookieStorage: fixture.jar,
            bootstrapSession: true,
            sessionConfiguration: configuration
        )
        fixture.transport.handler = { request, count in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            return count == 1 ? SessionRecoveryTransport.unauthorized : SessionRecoveryTransport.empty
        }
        let _: Empty = try await client.send(Endpoint(path: "/work"))
        XCTAssertEqual(fixture.transport.captured.count, 3)
        XCTAssertEqual(
            fixture.transport.captured.map { $0.value(forHTTPHeaderField: "User-Agent") },
            Array(repeating: "ProvidedNative/1.2 (existing client)", count: 3)
        )
    }

    func testStaleSignOutPreparationCannotClearCurrentJar() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let oldEpoch = fixture.client.sessionEpoch
        fixture.client.invalidateSessionRequests()
        do {
            _ = try await fixture.client.prepareSignOut(expectedEpoch: oldEpoch)
            XCTFail("Stale logout must not touch current credentials")
        } catch is CancellationError {}
        XCTAssertEqual(fixture.jar.cookies?.count, 2)
        XCTAssertTrue(fixture.transport.captured.isEmpty)
    }

    private func setup(
        persist: (@Sendable () throws -> Void)? = nil,
        signer: (any RequestSigning)? = nil
    ) throws -> Setup {
        let host = "recovery-\(UUID().uuidString.lowercased()).test"
        let url = URL(string: "http://\(host)")!
        let jar = URLSessionConfiguration.ephemeral.httpCookieStorage!
        for (name, value) in [("dt", "d-old"), ("st", "s-old")] {
            jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: name, .value: value])!)
        }
        XCTAssertEqual(jar.cookies(for: url)?.count, 2)
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        return try Setup(
            client: client(jar: jar, url: url, persist: persist, signer: signer),
            jar: jar,
            url: url,
            host: host,
            transport: transport
        )
    }

    private func client(
        jar: HTTPCookieStorage,
        url: URL,
        persist: (@Sendable () throws -> Void)? = nil,
        signer: (any RequestSigning)? = nil
    ) throws -> APIClient {
        APIClient(
            config: AppConfig(baseURL: url),
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: true,
            signer: signer,
            persistCookiesChecked: persist
        )
    }

    private struct Setup {
        let client: APIClient
        let jar: HTTPCookieStorage
        let url: URL
        let host: String
        let transport: SessionRecoveryTransport
    }
}

private actor RecoverySigner: RequestSigning {
    private var count = 0
    func signingHeaders(method _: String, path _: String, body _: Data?) async throws -> [String: String]? {
        count += 1
        return [RequestSignatureHeader.assertion: "assertion-\(count)"]
    }
}
