import Foundation
import Observation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
@testable import VouchaAuth
import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class SessionRecoveryIdentityTests: XCTestCase {
    func testAnonymousRotationNeverPublishesSignedInIdentity() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { request, _ in
            request.url!.path == "/api/v1/session" ? SessionRecoveryTransport
                .tokens(uid: nil) : SessionRecoveryTransport.unauthorized
        }
        let restored = await fixture.manager.refresh()
        XCTAssertFalse(restored)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.currentUserId)
        XCTAssertEqual(
            fixture.transport.captured.map { $0.url!.path },
            ["/api/v1/session"]
        )
    }

    func testLogoutFencesDelayedIdentityAndCookieResponse() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "identity pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            if request.url!.path == "/api/v1/my/identity" { entered.fulfill()
                return nil
            }
            return SessionRecoveryTransport.empty
        }
        let pending = Task { await fixture.manager.refresh() }
        await fulfillment(of: [entered], timeout: 5)
        await fixture.manager.signOut()
        fixture.transport.release(.init(
            status: 200,
            body: ApiFixtureLoader.data("swift.my.identity.default"),
            headers: ["Set-Cookie": "st=late; Path=/; HttpOnly"]
        ))
        let result = await pending.value
        XCTAssertFalse(result)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertFalse(fixture.manager.isRefreshing)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testAccountReplacementFencesDelayedIdentityPublication() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "identity pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            entered.fulfill()
            return nil
        }
        let pending = Task { await fixture.manager.refresh() }
        await fulfillment(of: [entered], timeout: 5)
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        fixture.transport.release(.init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default")))
        let result = await pending.value
        XCTAssertFalse(result)
        XCTAssertEqual(fixture.manager.currentUserId, identity.id)
        XCTAssertFalse(fixture.manager.isRefreshing)
        XCTAssertNil(fixture.manager.lastRefreshErrorMessage)
    }

    func testConcurrentLifecycleRefreshesAwaitSameIdentitySnapshot() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "identity pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
            entered.fulfill()
            return nil
        }
        let first = Task { await fixture.manager.refresh() }
        await fulfillment(of: [entered], timeout: 5)
        let joined = expectation(description: "second lifecycle caller")
        let second = Task { @MainActor in
            joined.fulfill()
            return await fixture.manager.refresh()
        }
        await fulfillment(of: [joined], timeout: 5)
        fixture.transport.release(.init(status: 200, body: ApiFixtureLoader.data("swift.my.identity.default")))
        let firstResult = await first.value
        let secondResult = await second.value
        XCTAssertTrue(firstResult)
        XCTAssertTrue(secondResult)
        XCTAssertEqual(fixture.transport.captured.count, 2)
    }

    func testLogoutSuppressesLifecycleRefreshWhileAuthorizedCleanupIsPending() async throws {
        let entered = expectation(description: "logout cleanup pending")
        let gate = SignOutGate()
        let fixture = try setup(beforeClear: { entered.fulfill()
            await gate.wait()
        })
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.empty }
        let signOut = Task { await fixture.manager.signOut() }
        await fulfillment(of: [entered], timeout: 5)
        let refreshed = await fixture.manager.refresh()
        XCTAssertFalse(refreshed)
        XCTAssertTrue(fixture.transport.captured.isEmpty)
        await gate.open()
        await signOut.value
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/api/v1/auth/logout"])
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testFailedDurableRotationClearsPreviouslySignedInSnapshot() async throws {
        let fixture = try setup(persist: { throw CocoaError(.fileWriteNoPermission) })
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens() }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        let refreshed = await fixture.manager.refresh()
        XCTAssertFalse(refreshed)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testOrdinaryFeatureTerminalFailureImmediatelyInvalidatesPublishedIdentity() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        let invalidated = expectation(description: "signed-in snapshot invalidated")
        withObservationTracking { _ = fixture.manager.isSignedIn } onChange: { invalidated.fulfill() }
        fixture.transport.handler = { _, _ in .init(
            status: 401,
            body: Data("{\"code\":\"SESSION_RELOGIN_REQUIRED\"}".utf8)
        ) }
        do {
            let _: IdentityEnvelope = try await fixture.client.send(Endpoint(path: "/protected-feature"))
            XCTFail("Expected sign-in requirement")
        } catch VouchaError.unauthorized {}
        await fulfillment(of: [invalidated], timeout: 5)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.uiLocale)
    }

    func testQueuedOldInvalidationCannotReplaceNewPublishedAccount() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        let coordinator = await fixture.client.cookieCoordinator
        try coordinator.clear(persist: nil)
        let old = coordinator.stamp
        fixture.manager.synchronize(with: identity)
        fixture.manager.applySessionInvalidation(epoch: old.epoch, revision: old.revision)
        XCTAssertEqual(fixture.manager.currentUserId, identity.id)
        XCTAssertTrue(fixture.manager.isSignedIn)
    }

    func testSessionManagerObservationDoesNotRetainItsOwner() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        weak var released: SessionManager?
        var manager: SessionManager? = SessionManager(client: fixture.client)
        released = manager
        manager = nil
        XCTAssertNil(released)
        try await fixture.client.clearSessionCookiesChecked()
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
    }

    func testAuthorizedDeviceCleanupUsesSameHTTPClientBeforeLogout() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.empty }
        fixture.manager.setBeforeSignOut { _ in
            let _: EmptyResponse = try await fixture.client.send(Endpoint(.DELETE, path: "/device-grant-cleanup"))
        }
        await fixture.manager.signOut()
        XCTAssertNil(fixture.manager.lastSignOutErrorMessage)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertEqual(
            fixture.transport.captured.map { $0.url!.path },
            ["/device-grant-cleanup", "/api/v1/auth/logout"]
        )
        XCTAssertEqual(fixture.transport.captured.last?.value(forHTTPHeaderField: "Cookie"), "dt=old-device")
    }

    func testDelayedLogoutCannotClearNewAccountTokensOrIdentity() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let entered = expectation(description: "old logout pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/auth/logout" { entered.fulfill()
                return nil
            }
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("new") }
            return .init(
                status: 200,
                body: Data("{}".utf8),
                headers: ["Set-Cookie": "st=new-account; Path=/; HttpOnly"]
            )
        }
        let logout = Task { await fixture.manager.signOut() }
        await fulfillment(of: [entered], timeout: 5)
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        let _: EmptyResponse = try await fixture.client.send(Endpoint(.POST, path: "/new-authentication"))
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        fixture.transport.release(.init(
            status: 200,
            body: Data("{}".utf8),
            headers: ["Set-Cookie": "st=; Max-Age=0; Path=/"]
        ))
        await logout.value
        XCTAssertTrue(fixture.manager.isSignedIn)
        XCTAssertEqual(fixture.manager.currentUserId, identity.id)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "new-account")
    }

    func testCheckedClearIsIndependentOfDisabledRenewalWrites() async throws {
        let fixture = try setup(persist: { throw CocoaError(.fileWriteNoPermission) }, clear: {})
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.empty }
        await fixture.manager.signOut()
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.lastSignOutErrorMessage)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/api/v1/auth/logout"])
    }

    func testSignOutReportsDurableClearFailureButStillRevokesOldServerSession() async throws {
        let fixture = try setup(persist: { throw CocoaError(.fileWriteNoPermission) })
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.empty }
        await fixture.manager.signOut()
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNotNil(fixture.manager.lastSignOutErrorMessage)
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/api/v1/auth/logout"])
    }

    func testSignOutOnlyClearsTheAPIClientsExactJar() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.empty }
        let unrelated = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        try unrelated.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .domain: "unrelated.test",
            .path: "/",
            .name: "st",
            .value: "unrelated-account"
        ])))
        await fixture.manager.signOut()
        XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        XCTAssertEqual(unrelated.cookies?.map(\.value), ["unrelated-account"])
    }

    func testConcurrentAuthenticationDuringCleanupAbortsOldLocalClear() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        fixture.transport.handler = { _, _ in .init(
            status: 200,
            body: Data("{}".utf8),
            headers: ["Set-Cookie": "st=new-account; Path=/; HttpOnly"]
        ) }
        fixture.manager.setBeforeSignOut { _ in
            let _: EmptyResponse = try await fixture.client.send(Endpoint(.POST, path: "/new-authentication"))
        }
        await fixture.manager.signOut()
        XCTAssertNotNil(fixture.manager.lastSignOutErrorMessage)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "new-account")
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/new-authentication"])
        fixture.manager.synchronize(with: identity)
        XCTAssertTrue(fixture.manager.isSignedIn)
    }

    func testDelayedClearStillInvalidatesOldIdentityAfterAnonymousBootstrapRevisionAdvances() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let identity = try APIClient.makeDecoder().decode(
            IdentityEnvelope.self,
            from: ApiFixtureLoader.data("swift.my.identity.default")
        ).identity
        fixture.manager.synchronize(with: identity)
        let coordinator = await fixture.client.cookieCoordinator
        try coordinator.clear(persist: nil)
        let cleared = coordinator.stamp
        let anonymous = try XCTUnwrap(HTTPCookie(properties: [
            .domain: fixture.host,
            .path: "/",
            .name: "dt",
            .value: "anonymous-device"
        ]))
        try coordinator.commit([anonymous], stamp: cleared, persist: nil, rotated: true, authenticated: false)
        XCTAssertTrue(fixture.manager.isSignedIn)
        fixture.manager.applySessionInvalidation(epoch: cleared.epoch, revision: cleared.revision)
        XCTAssertFalse(fixture.manager.isSignedIn)
    }

    private func setup(
        beforeClear: (@Sendable () async throws -> Void)? = nil,
        persist: (@Sendable () throws -> Void)? = nil,
        clear: (@Sendable () throws -> Void)? = nil
    ) throws -> Setup {
        let host = "identity-\(UUID().uuidString.lowercased()).test"
        let jar = URLSessionConfiguration.ephemeral.httpCookieStorage!
        jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: "dt", .value: "old-device"])!)
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://\(host)")!),
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: true,
            persistCookiesChecked: persist,
            clearCookiesChecked: clear
        )
        return Setup(
            manager: SessionManager(client: client, beforeClearSessionCookies: beforeClear),
            client: client,
            jar: jar,
            host: host,
            transport: transport
        )
    }

    private struct IdentityEnvelope: Decodable { let identity: PrivateUser }
    private struct Setup {
        let manager: SessionManager
        let client: APIClient
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
    }
}

private actor SignOutGate {
    private var continuation: CheckedContinuation<Void, Never>?
    func wait() async {
        await withCheckedContinuation { continuation = $0 }
    }

    func open() {
        continuation?.resume()
        continuation = nil
    }
}
