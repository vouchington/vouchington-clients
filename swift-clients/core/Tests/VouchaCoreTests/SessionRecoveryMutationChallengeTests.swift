import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class SessionRecoveryMutationChallengeTests: XCTestCase {
    private struct Empty: Decodable {}

    func testTypedAndRawRecoveryThenChallengeUseCurrentCredentialsWithoutReencoding() async throws {
        try await assertSuccessfulOrder(recoveryFirst: true)
    }

    func testTypedAndRawChallengeThenRecoveryRefreshCookiesAndSigningWithoutReencoding() async throws {
        try await assertSuccessfulOrder(recoveryFirst: false)
    }

    func testTypedAndRawChallengeReplayCannotStartASecondCredentialRotation() async throws {
        for raw in [false, true] {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
                let attempt = fixture.transport.captured.filter { $0.url!.path == "/mutation" }.count
                return attempt == 2 ? Self.challenge : SessionRecoveryTransport.unauthorized
            }
            let prompts = ProofCounter()
            await fixture.client.setMutationChallengeHandler { prompts.record()
                return "proof"
            }
            do {
                try await send(fixture.client, endpoint: Endpoint(.POST, path: "/mutation"), raw: raw)
                XCTFail("The second unauthorized response must surface without another renewal")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, [
                "/mutation", "/api/v1/session", "/mutation", "/mutation"
            ])
            XCTAssertEqual(prompts.count, 1)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        }
    }

    func testAttestedTypedAndRawMutationChallengesNeverReuseEitherOneUseHeader() async throws {
        for raw in [false, true] {
            for header in [RequestSignatureHeader.challengeId, RequestSignatureHeader.assertion] {
                let fixture = try setup()
                defer { SessionRecoveryProtocol.remove(fixture.host) }
                fixture.transport.handler = { _, _ in Self.challenge }
                await fixture.client.setMutationChallengeHandler {
                    XCTFail("A one-use App Attest request must not prompt or replay")
                    return "proof"
                }
                do {
                    try await send(fixture.client, endpoint: Endpoint(
                        .POST, path: "/mutation", headers: [header: "one-use-attestation"]
                    ), raw: raw)
                    XCTFail("Expected the original forbidden response")
                } catch VouchaError.forbidden {}
                XCTAssertEqual(fixture.transport.captured.count, 1)
                XCTAssertEqual(
                    fixture.transport.captured.first?.value(forHTTPHeaderField: header),
                    "one-use-attestation"
                )
            }
        }
    }

    func testInvalidationWhilePromptingFencesReplayEvenWhenCookieValuesAreUnchanged() async throws {
        for raw in [false, true] {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { _, _ in Self.challenge }
            await fixture.client.setMutationChallengeHandler {
                fixture.client.fenceSessionRequests()
                return "proof"
            }
            do {
                try await send(fixture.client, endpoint: Endpoint(.POST, path: "/mutation"), raw: raw)
                XCTFail("An invalidated owner cannot send the proof replay")
            } catch is CancellationError {}
            XCTAssertEqual(fixture.transport.captured.count, 1)
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-old")
        }
    }

    private func assertSuccessfulOrder(recoveryFirst: Bool) async throws {
        for raw in [false, true] {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { request, _ in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
                let attempt = fixture.transport.captured.filter { $0.url!.path == "/mutation" }.count
                if attempt == (recoveryFirst ? 1 : 2) { return SessionRecoveryTransport.unauthorized }
                if attempt == (recoveryFirst ? 2 : 1) { return Self.challenge }
                return .init(
                    status: 200,
                    body: Data("{}".utf8),
                    headers: ["Set-Cookie": "response_session=accepted; Path=/"]
                )
            }
            let prompts = ProofCounter()
            await fixture.client.setMutationChallengeHandler { prompts.record()
                return "proof"
            }
            let body = EncodingCounter()
            try await send(fixture.client, endpoint: Endpoint(.PATCH, path: "/mutation", body: body), raw: raw)
            let requests = fixture.transport.captured
            let mutations = requests.filter { $0.url!.path == "/mutation" }
            XCTAssertEqual(requests.map { $0.url!.path }, recoveryFirst ? [
                "/mutation", "/api/v1/session", "/mutation", "/mutation"
            ] : ["/mutation", "/mutation", "/api/v1/session", "/mutation"])
            XCTAssertEqual(body.count, 1)
            XCTAssertEqual(Set(mutations.compactMap(\.httpBody)).count, 1)
            XCTAssertEqual(Set(mutations.compactMap { $0.value(forHTTPHeaderField: "x-test-signature") }).count, 3)
            XCTAssertEqual(prompts.count, 1)
            XCTAssertTrue(mutations[0].value(forHTTPHeaderField: "Cookie")?.contains("st=s-old") == true)
            XCTAssertTrue(mutations[2].value(forHTTPHeaderField: "Cookie")?.contains("st=s-fresh") == true)
            XCTAssertEqual(mutations[2].value(forHTTPHeaderField: "x-voucha-bot-challenge-token"), "proof")
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "st" }?.value, "s-fresh")
            XCTAssertEqual(fixture.jar.cookies?.first { $0.name == "response_session" }?.value, "accepted")
        }
    }

    private func send(_ client: APIClient, endpoint: Endpoint, raw: Bool) async throws {
        if raw { _ = try await client.data(for: endpoint) }
        else { let _: Empty = try await client.send(endpoint) }
    }

    private func setup() throws -> Fixture {
        let host = "mutation-recovery-\(UUID().uuidString.lowercased()).test"
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        for (name, value) in [("dt", "d-old"), ("st", "s-old")] {
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
            protocolClasses: [SessionRecoveryProtocol.self], bootstrapSession: true,
            signer: CountingSigner(), sessionConfiguration: configuration
        )
        return Fixture(client: client, jar: jar, host: host, transport: transport)
    }

    private static var challenge: SessionRecoveryTransport.Reply {
        .init(
            status: 403,
            body: Data(#"{"code":"BOT_CHALLENGE_REQUIRED","action":"bot-challenge.mutation","message":"Verify"}"#.utf8)
        )
    }

    private struct Fixture {
        let client: APIClient
        let jar: HTTPCookieStorage
        let host: String
        let transport: SessionRecoveryTransport
    }
}

private actor CountingSigner: RequestSigning {
    private var count = 0
    func signingHeaders(method _: String, path _: String, body _: Data?) async throws -> [String: String]? {
        count += 1
        return ["x-test-signature": String(count)]
    }
}

private final class ProofCounter: @unchecked Sendable {
    private let lock = NSLock()
    private var calls = 0
    func record() {
        lock.withLock { calls += 1 }
    }

    var count: Int {
        lock.withLock { calls }
    }
}

private final class EncodingCounter: Encodable, @unchecked Sendable {
    private let lock = NSLock()
    private var encodes = 0
    var count: Int {
        lock.withLock { encodes }
    }

    func encode(to encoder: any Encoder) throws {
        let value = lock.withLock { encodes += 1
            return encodes
        }
        var container = encoder.singleValueContainer()
        try container.encode(value)
    }
}
