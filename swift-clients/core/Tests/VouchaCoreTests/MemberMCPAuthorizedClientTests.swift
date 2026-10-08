import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import XCTest

final class MemberMCPAuthorizedClientTests: XCTestCase {
    func testLateUnauthorizedForOldTokenReusesCompletedRotation() async throws {
        let bothOldRequests = expectation(description: "both old-token requests arrived")
        let firstRetry = expectation(description: "first rotated-token retry arrived")
        LateMCPURLProtocol.configure(arrival: bothOldRequests, firstRetry: firstRetry)
        let site = try XCTUnwrap(URL(string: "https://example.test"))
        let metadata = try MemberMCPOAuthMetadata(
            issuerIdentifier: "https://example.test",
            authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize")),
            tokenEndpoint: XCTUnwrap(URL(string: "https://example.test/token")),
            revocationEndpoint: XCTUnwrap(URL(string: "https://example.test/revoke"))
        )
        let store = AuthorizedMCPTokenStore()
        let tokens = try MemberMCPTokenManager(
            accountId: "member-1", store: store,
            tokenClient: MemberMCPOAuthTokenClient(
                metadata: metadata,
                clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos"),
                resource: site.appendingPathComponent("api/v1/mcp"),
                protocolClasses: [LateMCPURLProtocol.self]
            )
        )
        let client = try MemberMCPAuthorizedClient(
            client: MemberMCPClient(siteOrigin: site, protocolClasses: [LateMCPURLProtocol.self]),
            tokens: tokens
        )
        let otherClient = try MemberMCPAuthorizedClient(
            client: MemberMCPClient(siteOrigin: site, protocolClasses: [LateMCPURLProtocol.self]),
            tokens: tokens
        )
        let first = Task { try await client.listTools() }
        let second = Task { try await otherClient.listTools() }
        do {
            await fulfillment(of: [bothOldRequests], timeout: 10)
            LateMCPURLProtocol.releaseFirst()
            await fulfillment(of: [firstRetry], timeout: 10)
            LateMCPURLProtocol.releaseSecond()
            _ = try await first.value
            _ = try await second.value
        } catch {
            LateMCPURLProtocol.releaseFirst()
            LateMCPURLProtocol.releaseSecond()
            _ = try? await first.value
            _ = try? await second.value
            throw error
        }
        let snapshot = LateMCPURLProtocol.snapshot()
        XCTAssertEqual(snapshot.refreshCalls, 1)
        XCTAssertEqual(snapshot.bearers, [
            "Bearer old-access", "Bearer old-access", "Bearer new-access", "Bearer new-access"
        ])
        let saved = try await store.load(accountId: "member-1")
        XCTAssertEqual(saved?.refreshToken, "new-refresh")
    }

    func testMCP401RefreshesOnceThenUsesRotatedTokenOrRequiresAuthorization() async throws {
        for secondUnauthorized in [false, true] {
            AuthorizedMCPURLProtocol.configure(secondUnauthorized: secondUnauthorized)
            let site = try XCTUnwrap(URL(string: "https://example.test"))
            let metadata = try MemberMCPOAuthMetadata(
                issuerIdentifier: "https://example.test",
                authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize")),
                tokenEndpoint: XCTUnwrap(URL(string: "https://example.test/token")),
                revocationEndpoint: XCTUnwrap(URL(string: "https://example.test/revoke"))
            )
            let store = AuthorizedMCPTokenStore()
            let tokens = try MemberMCPTokenManager(
                accountId: "member-1", store: store,
                tokenClient: MemberMCPOAuthTokenClient(
                    metadata: metadata,
                    clientId: site.appendingPathComponent("api/v1/oauth/native-clients/macos"),
                    resource: site.appendingPathComponent("api/v1/mcp"),
                    protocolClasses: [AuthorizedMCPURLProtocol.self]
                )
            )
            let client = try MemberMCPAuthorizedClient(
                client: MemberMCPClient(
                    siteOrigin: site, protocolClasses: [AuthorizedMCPURLProtocol.self]
                ),
                tokens: tokens
            )
            if secondUnauthorized {
                do {
                    _ = try await client.listTools()
                    XCTFail("A second 401 must require new authorization")
                } catch MemberMCPTokenManager.Failure.notAuthorized {}
            } else {
                _ = try await client.listTools()
            }
            let snapshot = AuthorizedMCPURLProtocol.snapshot()
            XCTAssertEqual(snapshot.mcpCalls, 2)
            XCTAssertEqual(snapshot.refreshCalls, 1)
            XCTAssertEqual(snapshot.bearers, ["Bearer old-access", "Bearer new-access"])
            let saved = try await store.load(accountId: "member-1")
            XCTAssertEqual(saved?.refreshToken, secondUnauthorized ? nil : "new-refresh")
        }
    }
}

private final class LateMCPURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var arrival: XCTestExpectation?
    private static var firstRetry: XCTestExpectation?
    private static var pending: [LateMCPURLProtocol?] = []
    private static var oldRequests = 0
    private static var refreshCalls = 0
    private static var bearers: [String] = []

    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    static func configure(arrival: XCTestExpectation, firstRetry: XCTestExpectation) {
        lock.lock()
        self.arrival = arrival
        self.firstRetry = firstRetry
        pending = []
        oldRequests = 0
        refreshCalls = 0
        bearers = []
        lock.unlock()
    }

    static func snapshot() -> (refreshCalls: Int, bearers: [String]) {
        lock.lock()
        defer { lock.unlock() }
        return (refreshCalls, bearers)
    }

    static func releaseFirst() {
        release(at: 0)
    }

    static func releaseSecond() {
        release(at: 1)
    }

    private static func release(at index: Int) {
        lock.lock()
        let request = pending.indices.contains(index) ? pending[index] : nil
        if pending.indices.contains(index) { pending[index] = nil }
        lock.unlock()
        request?.complete(status: 401, body: #"{"jsonrpc":"2.0","id":"2","result":[]}"#)
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.lock()
        let isToken = request.url?.path == "/token"
        let bearer = request.value(forHTTPHeaderField: "Authorization") ?? ""
        if isToken {
            Self.refreshCalls += 1
        } else {
            Self.bearers.append(bearer)
            if bearer == "Bearer old-access" {
                Self.oldRequests += 1
                Self.pending.append(self)
                if Self.oldRequests == 2 { Self.arrival?.fulfill() }
                Self.lock.unlock()
                return
            }
            if bearer == "Bearer new-access", Self.bearers.filter({ $0 == bearer }).count == 1 {
                Self.firstRetry?.fulfill()
            }
        }
        Self.lock.unlock()
        if isToken {
            complete(
                status: 200,
                body: #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
            )
        } else {
            complete(status: 200, body: #"{"jsonrpc":"2.0","id":"2","result":[]}"#)
        }
    }

    private func complete(status: Int, body: String) {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped else { return }
        let response = HTTPURLResponse(
            url: request.url!, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        stopped = true
        deliveryLock.unlock()
    }
}

private actor AuthorizedMCPTokenStore: MemberMCPOAuthTokenStore {
    private var tokens: MemberMCPOAuthTokens? = MemberMCPOAuthTokens(
        accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
        scope: "mcp.user:read", tokenType: "Bearer"
    )
    func load(accountId _: String) async throws -> MemberMCPOAuthTokens? {
        tokens
    }

    func save(_ tokens: MemberMCPOAuthTokens, accountId _: String) async throws {
        self.tokens = tokens
    }

    func clear(accountId _: String) async throws {
        tokens = nil
    }
}

private final class AuthorizedMCPURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var secondUnauthorized = false
    private static var mcpCalls = 0
    private static var refreshCalls = 0
    private static var bearers: [String] = []

    static func configure(secondUnauthorized: Bool) {
        lock.lock()
        self.secondUnauthorized = secondUnauthorized
        mcpCalls = 0
        refreshCalls = 0
        bearers = []
        lock.unlock()
    }

    static func snapshot() -> (mcpCalls: Int, refreshCalls: Int, bearers: [String]) {
        lock.lock()
        defer { lock.unlock() }
        return (mcpCalls, refreshCalls, bearers)
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.lock()
        let isToken = request.url?.path == "/token"
        let status: Int
        let body: Data
        if isToken {
            Self.refreshCalls += 1
            status = 200
            body = Data(
                #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                    .utf8
            )
        } else {
            Self.mcpCalls += 1
            Self.bearers.append(request.value(forHTTPHeaderField: "Authorization") ?? "")
            status = Self.mcpCalls == 1 || Self.secondUnauthorized ? 401 : 200
            body = Data(#"{"jsonrpc":"2.0","id":"2","result":[]}"#.utf8)
        }
        Self.lock.unlock()
        let response = HTTPURLResponse(
            url: request.url!, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: body)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
