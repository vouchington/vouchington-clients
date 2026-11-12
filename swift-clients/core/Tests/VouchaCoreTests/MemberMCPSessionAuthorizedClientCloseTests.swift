import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

/// MCP transport that accepts requests and never answers, like a stalled server.
private final class StalledMCPProtocol: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private nonisolated(unsafe) static var count = 0

    static func reset() {
        lock.lock()
        count = 0
        lock.unlock()
    }

    static var requests: Int {
        lock.lock()
        defer { lock.unlock() }
        return count
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.lock()
        Self.count += 1
        Self.lock.unlock()
    }

    override func stopLoading() {}
}

final class MemberMCPSessionAuthorizedClientCloseTests: XCTestCase {
    func testCloseTimeoutBoundsTheReturnedTaskEvenWhenInitializationIsStalled() async throws {
        let host = "close-bound.test"
        SessionMCPMintProtocol.reset(host: host)
        SessionMCPMintProtocol.configure(host: host, resource: "https://\(host)/api/v1/mcp")
        StalledMCPProtocol.reset()
        let origin = try XCTUnwrap(URL(string: "https://\(host)"))
        let resource = try XCTUnwrap(URL(string: "https://\(host)/api/v1/mcp"))
        let api = APIClient(
            config: AppConfig(baseURL: origin, turnstileSiteKey: "key"),
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [SessionMCPMintProtocol.self]
        )
        let tokens = MemberMCPSessionTokenSource(
            client: api, app: .macos, resource: resource, isCurrent: { true }
        )
        let client = try MemberMCPClient(siteOrigin: origin, protocolClasses: [StalledMCPProtocol.self])
        let authorized = MemberMCPSessionAuthorizedClient(client: client, tokens: tokens)
        let stalled = Task { try? await authorized.listTools() }
        for _ in 0 ..< 500 where StalledMCPProtocol.requests == 0 {
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTAssertGreaterThan(StalledMCPProtocol.requests, 0, "initialization must be in flight")

        let close = await authorized.disconnect(closeTimeout: 0.3)
        let finished = expectation(description: "bounded close returns")
        Task {
            await close.value
            finished.fulfill()
        }
        await fulfillment(of: [finished], timeout: 3)
        stalled.cancel()
    }
}
