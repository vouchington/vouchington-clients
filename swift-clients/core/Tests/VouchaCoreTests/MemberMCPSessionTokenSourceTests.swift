import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class MemberMCPSessionTokenSourceTests: XCTestCase {
    private struct Signer: RequestSigning {
        func signingHeaders(method _: String, path _: String, body _: Data?) async throws -> [String: String]? {
            [RequestSignatureHeader.keyId: "key-1", RequestSignatureHeader.assertion: "assertion-1"]
        }
    }

    private final class Clock: @unchecked Sendable {
        private let lock = NSLock()
        private var date = Date(timeIntervalSince1970: 1_800_000_000)
        func now() -> Date {
            lock.lock()
            defer { lock.unlock() }
            return date
        }

        func advance(_ seconds: TimeInterval) {
            lock.lock()
            date += seconds
            lock.unlock()
        }
    }

    private final class Flag: @unchecked Sendable {
        private let lock = NSLock()
        private var value = true
        func get() -> Bool {
            lock.lock()
            defer { lock.unlock() }
            return value
        }

        func set(_ next: Bool) {
            lock.lock()
            value = next
            lock.unlock()
        }
    }

    private let host = "api.test"
    private let resource = URL(string: "https://api.test/api/v1/mcp")!

    override func setUp() {
        super.setUp()
        SessionMCPMintProtocol.reset(host: host)
    }

    private func source(
        signer: (any RequestSigning)? = nil, clock: Clock = Clock(), current: Flag = Flag()
    ) -> MemberMCPSessionTokenSource {
        let storage = IsolatedHTTPCookieStorage.make()
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "https://api.test")!, turnstileSiteKey: "key"),
            cookieStorage: storage, protocolClasses: [SessionMCPMintProtocol.self], signer: signer
        )
        return MemberMCPSessionTokenSource(
            client: client, app: .macos, resource: resource,
            isCurrent: { current.get() }, now: { clock.now() }
        )
    }

    func testMintsOnceWithNativeHeaderAndNoBrowserMetadata() async throws {
        let source = source()

        let first = try await source.accessToken()
        let second = try await source.accessToken()

        XCTAssertEqual(first, "token-1")
        XCTAssertEqual(second, "token-1")
        let request = try XCTUnwrap(SessionMCPMintProtocol.seen(host: host).first)
        XCTAssertEqual(SessionMCPMintProtocol.seen(host: host).count, 1)
        XCTAssertEqual(request.path, "/api/v1/oauth/native-session-tokens")
        XCTAssertEqual(request.headers["x-voucha-native-mcp-mint"], "1")
        XCTAssertEqual(String(decoding: request.body, as: UTF8.self), #"{"client_app":"macos"}"#)
        let names = request.headers.keys.map { $0.lowercased() }
        XCTAssertFalse(names.contains("origin"))
        XCTAssertFalse(names.contains { $0.hasPrefix("sec-fetch-") })
    }

    func testAppleMintCarriesTheAppAttestSignature() async throws {
        _ = try await source(signer: Signer()).accessToken()

        let headers = try XCTUnwrap(SessionMCPMintProtocol.seen(host: host).first).headers
        XCTAssertEqual(headers[RequestSignatureHeader.keyId.lowercased()], "key-1")
        XCTAssertEqual(headers[RequestSignatureHeader.assertion.lowercased()], "assertion-1")
    }

    func testMintsAgainAfterExpiry() async throws {
        let clock = Clock()
        let source = source(clock: clock)
        let first = try await source.accessToken()
        clock.advance(3_600)

        let second = try await source.accessToken()

        XCTAssertEqual([first, second], ["token-1", "token-2"])
    }

    func testConcurrentCallersShareOneMint() async throws {
        SessionMCPMintProtocol.configure(host: host, holding: true)
        let source = source()
        async let one = source.accessToken()
        async let two = source.accessToken()
        try await waitForRequests(1)

        SessionMCPMintProtocol.releaseHeld(host: host)

        let tokens = try await [one, two]
        XCTAssertEqual(tokens, ["token-1", "token-1"])
        XCTAssertEqual(SessionMCPMintProtocol.seen(host: host).count, 1)
    }

    func testRejectedTokenIsReplacedButANewerTokenIsReused() async throws {
        let source = source()
        let first = try await source.accessToken()

        let second = try await source.refresh(rejectedAccessToken: first)
        let reused = try await source.refresh(rejectedAccessToken: first)

        XCTAssertEqual([second, reused], ["token-2", "token-2"])
    }

    func testClearDropsTheTokenAndFencesAMintInFlight() async throws {
        SessionMCPMintProtocol.configure(host: host, holding: true)
        let source = source()
        let pending = Task { try await source.accessToken() }
        try await waitForRequests(1)

        await source.clear()
        // Release only after the transport has seen the cancellation; answering a request URLSession
        // already completed as cancelled resumes its continuation twice on newer Linux toolchains.
        for _ in 0 ..< 500 where SessionMCPMintProtocol.stopCount(host: host) < 1 {
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertGreaterThanOrEqual(SessionMCPMintProtocol.stopCount(host: host), 1)
        SessionMCPMintProtocol.releaseHeld(host: host)

        await assertNotAuthorized { _ = try await pending.value }
        await assertNotAuthorized { _ = try await source.accessToken() }
        let live = await source.currentAccessToken
        XCTAssertNil(live)
    }

    func testAccountChangeInvalidatesTheSource() async throws {
        let current = Flag()
        let source = source(current: current)
        _ = try await source.accessToken()

        current.set(false)

        await assertNotAuthorized { _ = try await source.accessToken() }
        let live = await source.currentAccessToken
        XCTAssertNil(live)
    }

    func testUnauthorizedMintIsNotAuthorizedAndForbiddenIsSurfaced() async throws {
        SessionMCPMintProtocol.configure(host: host, status: 401)
        await assertNotAuthorized { _ = try await source().accessToken() }

        SessionMCPMintProtocol.configure(host: host, status: 403)
        do {
            _ = try await source().accessToken()
            XCTFail("Expected the server gate to surface")
        } catch VouchaError.forbidden {
        } catch {
            XCTFail("Unexpected error \(error)")
        }
    }

    func testMismatchedMintResponseIsRejected() async throws {
        for (resource, tokenType, expiresIn) in [
            ("https://other.test/api/v1/mcp", "Bearer", 3_600), (nil, "MAC", 3_600), (nil, "Bearer", 0)
        ] {
            SessionMCPMintProtocol.configure(
                host: host, resource: resource, tokenType: tokenType, expiresIn: expiresIn
            )
            do {
                _ = try await source().accessToken()
                XCTFail("Expected the mint to be rejected")
            } catch VouchaError.unexpected {
            } catch {
                XCTFail("Unexpected error \(error)")
            }
        }
    }

    private func assertNotAuthorized(_ body: () async throws -> Void) async {
        do {
            try await body()
            XCTFail("Expected notAuthorized")
        } catch MemberMCPTokenManager.Failure.notAuthorized {
        } catch {
            XCTFail("Unexpected error \(error)")
        }
    }

    private func waitForRequests(_ count: Int) async throws {
        for _ in 0 ..< 500 where SessionMCPMintProtocol.seen(host: host).count < count {
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertGreaterThanOrEqual(SessionMCPMintProtocol.seen(host: host).count, count)
    }
}
