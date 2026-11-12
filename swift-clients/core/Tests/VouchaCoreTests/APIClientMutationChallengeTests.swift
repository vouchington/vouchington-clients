import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import VouchaTestSupport
import XCTest

final class APIClientMutationChallengeTests: XCTestCase {
    override func tearDown() {
        InvitationURLProtocol.reset()
        super.tearDown()
    }

    func testTypedAndRawReplayPreserveEncodedMutationAndStopAfterOneChallenge() async throws {
        for raw in [false, true] {
            InvitationURLProtocol.reset()
            InvitationURLProtocol.requestHandlers = ["PATCH /mutation": (challenge(), 403)]
            let client = try makeClient()
            await client.setMutationChallengeHandler { "proof" }
            let endpoint = Endpoint(.PATCH, path: "/mutation", body: ["value": "exact bytes"])
            do {
                if raw {
                    _ = try await client.data(for: endpoint)
                } else {
                    let _: EmptyResponse = try await client.send(endpoint)
                }
                XCTFail("Second challenge must surface")
            } catch VouchaError.forbidden {} catch { XCTFail("Unexpected error: \(error)") }
            XCTAssertEqual(InvitationURLProtocol.capturedMethods, ["PATCH", "PATCH"])
            XCTAssertEqual(InvitationURLProtocol.capturedURLs.count, 2)
            XCTAssertEqual(InvitationURLProtocol.capturedURLs.first, InvitationURLProtocol.capturedURLs.last)
            XCTAssertEqual(InvitationURLProtocol.capturedBodies.first, InvitationURLProtocol.capturedBodies.last)
        }
    }

    func testSuccessfulReplayCarriesOneUseProofAndFreshSignature() async throws {
        InvitationURLProtocol.requestHandlers = ["PATCH /mutation": (challenge(), 403)]
        let signer = MutationCountingSigner()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999")), turnstileSiteKey: "test"),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [InvitationURLProtocol.self],
            signer: signer
        )
        await client.setMutationChallengeHandler {
            InvitationURLProtocol.requestHandlers = ["PATCH /mutation": (Data("{}".utf8), 200)]
            return "one-use-proof"
        }
        let body = MutationEncodingCounter()
        let _: EmptyResponse = try await client.send(.init(.PATCH, path: "/mutation", body: body))
        XCTAssertEqual(body.count, 1)
        let headers = InvitationURLProtocol.capturedHeaders
        XCTAssertEqual(headers.count, 2)
        let first = headers[0].mapKeysLowercased()
        let replay = headers[1].mapKeysLowercased()
        XCTAssertNil(first["x-voucha-bot-challenge-token"])
        XCTAssertEqual(replay["x-voucha-bot-challenge-token"], "one-use-proof")
        XCTAssertEqual(first["x-test-signature"], "1")
        XCTAssertEqual(replay["x-test-signature"], "2")
    }

    func testOrdinaryForbiddenWrongActionReadAndBlankProofDoNotReplay() async throws {
        for (method, body, proof) in [
            ("POST", Data(#"{"code":"FORBIDDEN"}"#.utf8), "proof"),
            ("POST", challenge(action: "posts.create"), "proof"),
            ("GET", challenge(), "proof"),
            ("POST", challenge(), " \n")
        ] {
            InvitationURLProtocol.reset()
            InvitationURLProtocol.requestHandlers = ["\(method) /mutation": (body, 403)]
            let client = try makeClient()
            await client.setMutationChallengeHandler { proof }
            do {
                _ = try await client.data(for: .init(method == "GET" ? .GET : .POST, path: "/mutation"))
                XCTFail("Expected forbidden")
            } catch VouchaError.forbidden {} catch { XCTFail("Unexpected error: \(error)") }
            XCTAssertEqual(InvitationURLProtocol.capturedURLs.count, 1)
        }
    }

    func testProductSignalNeverPresentsInteractiveChallenge() async throws {
        InvitationURLProtocol.requestHandlers = ["POST /api/v1/rss-feeds/feed/views": (challenge(), 403)]
        let client = try makeClient()
        await client.setMutationChallengeHandler {
            XCTFail("Analytics must not present a challenge")
            return "proof"
        }
        do {
            let _: EmptyResponse = try await client.send(.rssFeedView(rssFeedId: "feed"))
            XCTFail("Expected forbidden")
        } catch VouchaError.forbidden {} catch { XCTFail("Unexpected error: \(error)") }
        XCTAssertEqual(InvitationURLProtocol.capturedURLs.count, 1)
    }

    func testSessionReplacementWhilePromptingDoesNotReplay() async throws {
        InvitationURLProtocol.requestHandlers = ["POST /mutation": (challenge(), 403)]
        let storage = IsolatedHTTPCookieStorage.make()
        let url = try XCTUnwrap(URL(string: "http://localhost:2999"))
        let replacement = try XCTUnwrap(HTTPCookie(properties: [
            .domain: "localhost", .path: "/", .name: "st", .value: "replacement"
        ]))
        let client = APIClient(
            config: AppConfig(baseURL: url, turnstileSiteKey: "test"),
            cookieStorage: storage, protocolClasses: [InvitationURLProtocol.self]
        )
        await client.setMutationChallengeHandler {
            storage.setCookie(replacement)
            return "old-session-proof"
        }
        do {
            _ = try await client.data(for: .init(.POST, path: "/mutation"))
            XCTFail("Expected forbidden")
        } catch VouchaError.forbidden {} catch { XCTFail("Unexpected error: \(error)") }
        XCTAssertEqual(InvitationURLProtocol.capturedURLs.count, 1)
    }

    private func challenge(action: String = "bot-challenge.mutation") -> Data {
        Data("{\"code\":\"BOT_CHALLENGE_REQUIRED\",\"action\":\"\(action)\",\"message\":\"Verify\"}".utf8)
    }

    private func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999")), turnstileSiteKey: "test"),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [InvitationURLProtocol.self]
        )
    }
}

private actor MutationCountingSigner: RequestSigning {
    private var count = 0
    func signingHeaders(method _: String, path _: String, body _: Data?) async throws -> [String: String]? {
        count += 1
        return ["x-test-signature": String(count)]
    }
}

private extension [String: String] {
    func mapKeysLowercased() -> [String: String] {
        Dictionary(uniqueKeysWithValues: map { ($0.key.lowercased(), $0.value) })
    }
}

private final class MutationEncodingCounter: Encodable, @unchecked Sendable {
    private let lock = NSLock()
    private var encodes = 0
    var count: Int {
        lock.lock()
        defer { lock.unlock() }
        return encodes
    }

    func encode(to encoder: any Encoder) throws {
        lock.lock()
        encodes += 1
        let value = encodes
        lock.unlock()
        var container = encoder.singleValueContainer()
        try container.encode(value)
    }
}
