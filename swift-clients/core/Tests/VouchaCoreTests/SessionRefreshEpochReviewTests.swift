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
final class SessionRefreshEpochReviewTests: XCTestCase {
    func testSuccessfulLoginRefreshesNewEpochBeforeOldIdentityResponseCompletes() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let oldIdentityPending = expectation(description: "old identity response held")
        let old = try identity(id: "old-account")
        let new = try identity(id: "new-account")
        fixture.manager.synchronize(with: old.user)
        fixture.transport.handler = { request, _ in
            switch request.url!.path {
            case "/api/v1/session":
                let loggingIn = request.value(forHTTPHeaderField: "Cookie")?.contains("st=s-login") == true
                return SessionRecoveryTransport.tokens(
                    loggingIn ? "new" : "old",
                    uid: loggingIn ? new.user.id : old.user.id
                )
            case "/api/v1/auth/native/email-address/login":
                return .init(
                    status: 200,
                    body: Data("{}".utf8),
                    headers: ["Set-Cookie": "st=s-login; Path=/; HttpOnly"]
                )
            default:
                if request.value(forHTTPHeaderField: "Cookie")?.contains("st=s-old") == true {
                    oldIdentityPending.fulfill()
                    return nil
                }
                return .init(status: 200, body: new.data)
            }
        }
        let oldRefresh = Task { await fixture.manager.refresh() }
        await fulfillment(of: [oldIdentityPending], timeout: 5)
        let authenticationCompleted = expectation(description: "new account published while old GET remains held")
        let signIn = Task {
            defer { authenticationCompleted.fulfill() }
            return try await fixture.service.verifyEmailOTP(email: "new@example.test", code: "ABCDEFGH23")
        }
        await fulfillment(of: [authenticationCompleted], timeout: 5)
        let publishedBeforeOldResponse = fixture.manager.currentUserId
        fixture.transport.release(.init(status: 200, body: old.data), path: "/api/v1/my/identity")
        switch await signIn.result {
        case let .success(challenge): XCTAssertNil(challenge)
        case let .failure(error): XCTFail("Successful login failed locally: \(error)")
        }
        let oldResult = await oldRefresh.value
        XCTAssertFalse(oldResult)
        XCTAssertEqual(publishedBeforeOldResponse, "new-account")
        XCTAssertEqual(fixture.manager.currentUserId, "new-account")
        XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, [
            "/api/v1/session", "/api/v1/my/identity", "/api/v1/auth/native/email-address/login",
            "/api/v1/session", "/api/v1/my/identity"
        ])
    }

    func testNewEpochRenewalFailureCannotCompleteLoginUsingOldPublishedIdentity() async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        try fixture.manager.synchronize(with: identity(id: "old-account").user)
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/auth/native/email-address/login" {
                return .init(
                    status: 200,
                    body: Data("{}".utf8),
                    headers: ["Set-Cookie": "st=s-login; Path=/; HttpOnly"]
                )
            }
            return .init(status: 503, body: Data("{}".utf8))
        }
        do {
            _ = try await fixture.service.verifyEmailOTP(email: "new@example.test", code: "ABCDEFGH23")
            XCTFail("A prior account must not make a failed new-epoch refresh look signed in")
        } catch VouchaError.unauthorized {}
        XCTAssertFalse(fixture.manager.isSignedIn)
        XCTAssertNil(fixture.manager.currentUserId)
        XCTAssertNotNil(fixture.manager.lastRefreshErrorMessage)
        XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-login")
    }

    private func identity(id: String) throws -> (user: PrivateUser, data: Data) {
        let fixture = ApiFixtureLoader.data("swift.my.identity.default")
        var envelope = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture) as? [String: Any])
        var identity = try XCTUnwrap(envelope["identity"] as? [String: Any])
        identity["id"] = id
        envelope["identity"] = identity
        let data = try JSONSerialization.data(withJSONObject: envelope)
        struct Envelope: Decodable { let identity: PrivateUser }
        return try (APIClient.makeDecoder().decode(Envelope.self, from: data).identity, data)
    }

    private func setup() throws -> Fixture {
        let host = "refresh-epoch-\(UUID().uuidString.lowercased()).test"
        let jar = URLSessionConfiguration.ephemeral.httpCookieStorage!
        for (name, value) in [("dt", "d-initial"), ("st", "s-initial")] {
            jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: name, .value: value])!)
        }
        let transport = SessionRecoveryTransport()
        SessionRecoveryProtocol.register(transport, host: host)
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://\(host)")!),
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: true
        )
        let manager = SessionManager(client: client)
        let service = SignInService(client: client, sessionManager: manager)
        return Fixture(manager: manager, service: service, jar: jar, host: host, transport: transport)
    }

    private struct Fixture {
        let manager: SessionManager
        let service: SignInService
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
    }
}
