import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import VouchaModels
import XCTest

@MainActor
final class SessionSynchronizationReviewTests: XCTestCase {
    func testProviderConnectionIdentityPreservesPendingRotationAndNextRefreshUsesReplacementPair() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let connected = try identity(id: "member", connected: true)
        let store = try NativeOAuthAuthorizationStore(secureState: UserDefaultsNativePendingState(
            key: "pending", defaults: XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
        ))
        let coordinator = NativeOAuthAuthorizationCoordinator(
            client: fixture.client, sessionManager: fixture.manager, store: store
        )
        let identityEntered = expectation(description: "connection identity GET pending")
        let rotationEntered = expectation(description: "credential rotation PATCH pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { rotationEntered.fulfill() }
            else { identityEntered.fulfill() }
            return nil
        }
        let connection = Task { await coordinator.confirmConnectedProvider(.github) }
        await fulfillment(of: [identityEntered], timeout: 5)
        let epoch = fixture.client.sessionEpoch
        let rotation = Task { try await fixture.client.refreshSession() }
        await fulfillment(of: [rotationEntered], timeout: 5)
        fixture.transport.release(.init(status: 200, body: connected.data), path: "/api/v1/my/identity")
        let confirmed = await connection.value
        XCTAssertTrue(confirmed)
        XCTAssertEqual(fixture.manager.currentUserId, connected.user.id)
        fixture.transport.release(
            SessionRecoveryTransport.tokens("renewed", uid: connected.user.id),
            path: "/api/v1/session"
        )
        try await rotation.value
        XCTAssertEqual(fixture.client.sessionEpoch, epoch)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-renewed")
        fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens("next", uid: connected.user.id) }
        try await fixture.client.refreshSession()
        XCTAssertEqual(
            try JSONSerialization
                .jsonObject(with: XCTUnwrap(fixture.transport.captured.last?.httpBody)) as? [String: String],
            ["dt": "d-renewed", "st": "s-renewed"]
        )
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, [
            "/api/v1/my/identity", "/api/v1/session", "/api/v1/session"
        ])
    }

    func testIdentitySynchronizationFencesStaleRefreshPublicationWithoutInvalidatingCredentials() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let stale = try identity(id: "stale-member")
        let current = try identity(id: "current-member")
        let identityEntered = expectation(description: "refresh identity GET pending")
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens("renewed") }
            identityEntered.fulfill()
            return nil
        }
        let refresh = Task { await fixture.manager.refresh() }
        await fulfillment(of: [identityEntered], timeout: 5)
        let epoch = fixture.client.sessionEpoch
        fixture.manager.synchronize(with: current.user)
        fixture.transport.release(.init(status: 200, body: stale.data), path: "/api/v1/my/identity")
        let refreshed = await refresh.value
        XCTAssertFalse(refreshed)
        XCTAssertEqual(fixture.manager.currentUserId, current.user.id)
        XCTAssertEqual(fixture.client.sessionEpoch, epoch)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-renewed")
    }

    private func identity(id: String, connected: Bool = false) throws -> (user: PrivateUser, data: Data) {
        var envelope = try XCTUnwrap(JSONSerialization.jsonObject(
            with: ApiFixtureLoader.data("swift.my.identity.default")
        ) as? [String: Any])
        var identity = try XCTUnwrap(envelope["identity"] as? [String: Any])
        identity["id"] = id
        if connected {
            identity["github_account"] = ["id": "github-member", "name": NSNull(), "email_address": NSNull()]
        }
        envelope["identity"] = identity
        let data = try JSONSerialization.data(withJSONObject: envelope)
        struct Envelope: Decodable { let identity: PrivateUser }
        return try (APIClient.makeDecoder().decode(Envelope.self, from: data).identity, data)
    }

    private func setup() throws -> Fixture {
        let host = "synchronization-\(UUID().uuidString.lowercased()).test"
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
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://\(host)"))), cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self], bootstrapSession: true, sessionConfiguration: configuration
        )
        return Fixture(
            client: client,
            manager: SessionManager(client: client),
            jar: jar,
            host: host,
            transport: transport
        )
    }

    private struct Fixture {
        let client: APIClient
        let manager: SessionManager
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
    }
}
