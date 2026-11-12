import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class SessionCredentialBoundaryMatrixTests: XCTestCase {
    private struct Empty: Decodable {}

    func testEveryCredentialBoundaryAndPublicFenceAgainstHeldPatchInBothCompletionOrders() async throws {
        for boundary in Boundary.allCases {
            for rotationFirst in [false, true] {
                try await exercise(boundary, rotationFirst: rotationFirst)
            }
        }
    }

    func testEveryRenewalFailureDisposesItsOwnedCredentialsAcrossPublicFenceOrdering() async throws {
        for failure in Failure.allCases {
            for fenceFirst in [false, true] {
                let fixture = try setup()
                defer { SessionRecoveryProtocol.remove(fixture.host) }
                let owner = await fixture.client.cookieCoordinator
                let ownership = owner.stamp.credentialOwnership
                let entered = expectation(description: "held PATCH failure \(failure) fenceFirst=\(fenceFirst)")
                fixture.transport.handler = { _, _ in entered.fulfill()
                    return nil
                }
                let pending = Task { try await fixture.client.refreshSession() }
                await fulfillment(of: [entered], timeout: 5)
                if fenceFirst { fixture.client.fenceSessionRequests() }
                if failure == .persistence { fixture.persistence.arm() }
                fixture.transport.release(failure.response, path: "/api/v1/session")
                do { try await pending.value
                    XCTFail("An unusable renewal must fail")
                } catch {}
                if !fenceFirst { fixture.client.fenceSessionRequests() }
                XCTAssertTrue(fixture.jar.cookies?.isEmpty == true, "\(failure) must dispose live credentials")
                XCTAssertEqual(fixture.persisted.snapshots.last, [:], "\(failure) must persist disposal")
                XCTAssertGreaterThan(owner.stamp.credentialOwnership, ownership)
            }
        }
    }

    private func exercise(_ boundary: Boundary, rotationFirst: Bool) async throws {
        let fixture = try setup()
        defer { SessionRecoveryProtocol.remove(fixture.host) }
        let owner = await fixture.client.cookieCoordinator
        let ownership = owner.stamp.credentialOwnership
        let entered = expectation(description: "held PATCH \(boundary) rotationFirst=\(rotationFirst)")
        let responseEntered = boundary.endpoint
            .map { _ in expectation(description: "boundary response held before PATCH") }
        fixture.transport.handler = { request, _ in
            if request.url!.path == "/api/v1/session" { entered.fulfill()
                return nil
            }
            responseEntered?.fulfill()
            return nil
        }
        var boundaryRequest: Task<Empty, Error>?
        if let endpoint = boundary.endpoint {
            boundaryRequest = Task { try await fixture.client.send(endpoint) }
            try await fulfillment(of: [XCTUnwrap(responseEntered)], timeout: 5)
        }
        let pending = Task { try await fixture.client.refreshSession() }
        await fulfillment(of: [entered], timeout: 5)
        if rotationFirst {
            fixture.transport.release(SessionRecoveryTransport.tokens(), path: "/api/v1/session")
            try await pending.value
        }
        let durableBefore = fixture.persisted.snapshots.last
        try await apply(boundary, fixture: fixture, pending: boundaryRequest, rotationFirst: rotationFirst)
        if !rotationFirst {
            fixture.transport.release(SessionRecoveryTransport.tokens(), path: "/api/v1/session")
            do { try await pending.value } catch { XCTAssertTrue(error is CancellationError) }
        }
        let disposition = rotationFirst && boundary.suppressedAfterRotation ? Disposition.retain : boundary.disposition
        switch disposition {
        case .retain:
            XCTAssertEqual(
                owner.stamp.credentialOwnership,
                ownership,
                "A public fence must not abandon rotation ownership"
            )
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-fresh")
            XCTAssertEqual(fixture.persisted.snapshots.last?["st"], "s-fresh")
            fixture.transport.handler = { _, _ in SessionRecoveryTransport.tokens("next") }
            try await fixture.client.refreshSession()
            XCTAssertEqual(
                try JSONDecoder().decode(
                    [String: String].self,
                    from: XCTUnwrap(fixture.transport.captured.last?.httpBody)
                ),
                ["dt": "d-fresh", "st": "s-fresh"]
            )
        case .replace:
            XCTAssertGreaterThan(owner.stamp.credentialOwnership, ownership)
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "replacement")
            XCTAssertEqual(fixture.persisted.snapshots.last?["st"], "replacement")
        case .discardFailedPersistence:
            XCTAssertGreaterThan(owner.stamp.credentialOwnership, ownership)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
            XCTAssertEqual(
                fixture.persisted.snapshots.last,
                durableBefore,
                "A reported failed disk deletion must not claim durable success"
            )
        case .discard:
            XCTAssertGreaterThan(owner.stamp.credentialOwnership, ownership)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
            XCTAssertEqual(fixture.persisted.snapshots.last, [:])
        }
    }

    private func apply(
        _ boundary: Boundary, fixture: Fixture, pending: Task<Empty, Error>?, rotationFirst: Bool
    ) async throws {
        switch boundary {
        case .publicFence: fixture.client.fenceSessionRequests()
        case .peerFence: fixture.peer.fenceSessionRequests()
        case .signOutBarrier:
            let owner = await fixture.client.cookieCoordinator
            owner.beginSignOut()
            owner.endSignOut()
        case .explicitClear: try await fixture.client.clearSessionCookiesChecked()
        case .explicitClearFailure:
            fixture.persistence.arm()
            do { try await fixture.client.clearSessionCookiesChecked()
                XCTFail("Expected durable clear failure")
            } catch { XCTAssertEqual((error as NSError).code, CocoaError.fileWriteNoPermission.rawValue) }
        case .preparedSignOut, .preparedSignOutClearFailure:
            if boundary == .preparedSignOutClearFailure { fixture.persistence.arm() }
            let captured = try await fixture.client.prepareSignOut(expectedEpoch: fixture.client.sessionEpoch)
            XCTAssertEqual(captured.persistenceError != nil, boundary == .preparedSignOutClearFailure)
        case .authenticatedReplacement, .ordinaryReplacement, .terminalResponse,
             .ordinaryPersistenceFailure, .authenticatedPersistenceFailure:
            let request = try XCTUnwrap(pending)
            let failedWrite = boundary == .ordinaryPersistenceFailure || boundary == .authenticatedPersistenceFailure
            if failedWrite { fixture.persistence.arm() }
            let endpoint = try XCTUnwrap(boundary.endpoint)
            fixture.transport.release(boundary.response, path: endpoint.path)
            if boundary == .terminalResponse {
                do { _ = try await request.value
                    XCTFail("Expected terminal or superseded response")
                } catch {
                    if rotationFirst { XCTAssertTrue(error is CancellationError) }
                    else if case VouchaError.unauthorized = error {}
                    else { XCTFail("Unexpected terminal error: \(error)") }
                }
            } else if failedWrite, !(rotationFirst && boundary.suppressedAfterRotation) {
                do { _ = try await request.value
                    XCTFail("Expected durable replacement failure")
                } catch { XCTAssertEqual((error as NSError).code, CocoaError.fileWriteNoPermission.rawValue) }
            } else {
                _ = try await request.value
            }
            if boundary == .ordinaryPersistenceFailure, rotationFirst {
                XCTAssertTrue(fixture.persistence.consume(), "The stale response must never attempt a snapshot write")
            }
        }
    }

    private func setup() throws -> Fixture {
        let host = "ownership-matrix-\(UUID().uuidString.lowercased()).test"
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
        persisted.snapshot()
        let persistence = OwnershipPersistenceGate()
        let config = try AppConfig(baseURL: XCTUnwrap(URL(string: "http://\(host)")))
        let client = APIClient(
            config: config, cookieStorage: jar, protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: true,
            sessionConfiguration: configuration, persistCookiesChecked: {
                if persistence.consume() { throw CocoaError(.fileWriteNoPermission) }
                persisted.snapshot()
            }
        )
        let peer = APIClient(
            config: config,
            cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self],
            bootstrapSession: true,
            sessionConfiguration: configuration
        )
        return Fixture(
            client: client,
            peer: peer,
            jar: jar,
            host: host,
            transport: transport,
            persisted: persisted,
            persistence: persistence
        )
    }

    private enum Disposition { case retain, replace, discard, discardFailedPersistence }
    private enum Boundary: CaseIterable {
        case publicFence, peerFence, signOutBarrier, explicitClear, preparedSignOut
        case explicitClearFailure, preparedSignOutClearFailure
        case authenticatedReplacement, ordinaryReplacement, terminalResponse
        case ordinaryPersistenceFailure, authenticatedPersistenceFailure
        var endpoint: Endpoint? {
            switch self {
            case .authenticatedReplacement, .authenticatedPersistenceFailure:
                Endpoint(.POST, path: "/api/v1/auth/email-address/login")
            case .ordinaryReplacement, .terminalResponse, .ordinaryPersistenceFailure:
                Endpoint(path: "/feature")
            default: nil
            }
        }

        var response: SessionRecoveryTransport.Reply {
            if self == .terminalResponse {
                return .init(status: 401, body: Data(#"{"code":"SESSION_RELOGIN_REQUIRED"}"#.utf8))
            }
            let cookie = self == .ordinaryPersistenceFailure ? "preference=changed" : "st=replacement"
            return .init(status: 200, body: Data("{}".utf8), headers: ["Set-Cookie": "\(cookie); Path=/; HttpOnly"])
        }

        var suppressedAfterRotation: Bool {
            self == .ordinaryReplacement || self == .terminalResponse || self == .ordinaryPersistenceFailure
        }

        var disposition: Disposition {
            switch self {
            case .publicFence, .peerFence, .signOutBarrier: .retain
            case .authenticatedReplacement, .ordinaryReplacement: .replace
            case .explicitClearFailure, .preparedSignOutClearFailure: .discardFailedPersistence
            default: .discard
            }
        }
    }

    private enum Failure: CaseIterable {
        case malformed, terminal, anonymous, persistence
        var response: SessionRecoveryTransport.Reply {
            switch self {
            case .malformed: .init(status: 200, body: Data("{".utf8))
            case .terminal: .init(status: 401, body: Data(#"{"code":"SESSION_REFRESH_REUSED"}"#.utf8))
            case .anonymous: SessionRecoveryTransport.tokens("anonymous", uid: nil)
            case .persistence: SessionRecoveryTransport.tokens()
            }
        }
    }

    private struct Fixture {
        let client: APIClient
        let peer: APIClient
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
        let persisted: CookiePersistenceRecorder
        let persistence: OwnershipPersistenceGate
    }
}

private final class OwnershipPersistenceGate: @unchecked Sendable {
    private let lock = NSLock()
    private var armed = false
    func arm() {
        lock.withLock { armed = true }
    }

    func consume() -> Bool {
        lock.withLock {
            let result = armed
            armed = false
            return result
        }
    }
}
