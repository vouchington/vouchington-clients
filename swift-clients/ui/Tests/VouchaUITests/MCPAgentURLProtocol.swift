import Foundation
import VouchaTestSupport

final class MCPAgentURLProtocol: URLProtocol {
    private static let stateLock = NSLock()
    private static var unavailable = false
    private static var paginatedTools = false
    private static var emptyContinuationCursor = false
    private static var revoked = 0
    private static var tokenRequests = 0
    private static var shouldHoldClose = false
    private static var shouldHoldDiscovery = false
    private static var heldDiscovery: MCPAgentURLProtocol?
    private static var shouldHoldRevoke = false
    private static var heldRevoke: MCPAgentURLProtocol?
    private static var heldClose: MCPAgentURLProtocol?
    private static var closeWaiters: [CheckedContinuation<Void, Never>] = []
    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    static func configure(
        discoveryUnavailable: Bool = false, paginatedTools: Bool = false,
        emptyContinuationCursor: Bool = false, holdClose: Bool = false,
        holdDiscovery: Bool = false, holdRevoke: Bool = false
    ) {
        stateLock.lock()
        unavailable = discoveryUnavailable
        self.paginatedTools = paginatedTools
        self.emptyContinuationCursor = emptyContinuationCursor
        revoked = 0
        tokenRequests = 0
        shouldHoldClose = holdClose
        shouldHoldDiscovery = holdDiscovery
        heldDiscovery = nil
        shouldHoldRevoke = holdRevoke
        heldRevoke = nil
        heldClose = nil
        closeWaiters = []
        stateLock.unlock()
    }

    static var isCloseHeld: Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        return heldClose != nil
    }

    static var isDiscoveryHeld: Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        return heldDiscovery != nil
    }

    static var isRevokeHeld: Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        return heldRevoke != nil
    }

    static func releaseHeldDiscovery() {
        stateLock.lock()
        let pending = heldDiscovery
        heldDiscovery = nil
        shouldHoldDiscovery = false
        stateLock.unlock()
        let payload = """
        {"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://example.test"]}
        """
        pending?.finishHeld(payload: payload)
    }

    static func releaseHeldRevoke() {
        stateLock.lock()
        let pending = heldRevoke
        heldRevoke = nil
        shouldHoldRevoke = false
        stateLock.unlock()
        pending?.finishHeld(payload: "")
    }

    static func waitForHeldClose() async {
        await withCheckedContinuation { waiter in
            stateLock.lock()
            if heldClose != nil {
                stateLock.unlock()
                waiter.resume()
            } else {
                closeWaiters.append(waiter)
                stateLock.unlock()
            }
        }
    }

    static func releaseHeldClose() {
        stateLock.lock()
        let pending = heldClose
        heldClose = nil
        shouldHoldClose = false
        stateLock.unlock()
        pending?.finishClose()
    }

    static var revokeCount: Int {
        stateLock.lock()
        defer { stateLock.unlock() }
        return revoked
    }

    static var tokenRequestCount: Int {
        stateLock.lock()
        defer { stateLock.unlock() }
        return tokenRequests
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped, let url = request.url else { return }
        let path = url.path
        let body = URLRequestHTTPBody.data(from: request)
        let rpc = body.flatMap { (try? JSONSerialization.jsonObject(with: $0)) as? [String: Any] }
        let method = rpc?["method"] as? String
        let cursor = (rpc?["params"] as? [String: Any])?["cursor"] as? String
        let id = rpc?["id"] as? String ?? "1"
        Self.stateLock.lock()
        let discoveryUnavailable = Self.unavailable
        let paginatedTools = Self.paginatedTools
        let emptyContinuationCursor = Self.emptyContinuationCursor
        let sessionful = Self.shouldHoldClose
        if path == "/.well-known/oauth-protected-resource/api/v1/mcp", Self.shouldHoldDiscovery {
            Self.heldDiscovery = self
            Self.stateLock.unlock()
            return
        }
        if path == "/revoke", Self.shouldHoldRevoke {
            Self.revoked += 1
            Self.heldRevoke = self
            Self.stateLock.unlock()
            return
        }
        if path == "/api/v1/mcp", request.httpMethod == "DELETE", Self.shouldHoldClose {
            Self.heldClose = self
            let waiters = Self.closeWaiters
            Self.closeWaiters = []
            Self.stateLock.unlock()
            for waiter in waiters {
                waiter.resume()
            }
            return
        }
        if path == "/revoke" { Self.revoked += 1 }
        if path == "/token" { Self.tokenRequests += 1 }
        Self.stateLock.unlock()
        let payload: String
        let status: Int
        switch path {
        case "/.well-known/oauth-protected-resource/api/v1/mcp" where discoveryUnavailable:
            payload = #"{"error":"unavailable"}"#
            status = 503
        case "/.well-known/oauth-protected-resource/api/v1/mcp":
            payload = #"{"resource":"https://example.test/api/v1/mcp","authorization_servers":["https://example.test"]}"#
            status = 200
        case "/.well-known/oauth-authorization-server":
            payload = #"{"issuer":"https://example.test","authorization_endpoint":"https://example.test/authorize","token_endpoint":"https://example.test/token","revocation_endpoint":"https://example.test/revoke"}"#
            status = 200
        case "/token":
            payload = #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
            status = 200
        case "/revoke":
            payload = ""
            status = 200
        case "/api/v1/mcp" where request.httpMethod == "DELETE":
            payload = ""
            status = 204
        case "/api/v1/mcp" where method == "initialize":
            payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"protocolVersion":"2025-06-18"}}"#
            status = 200
        case "/api/v1/mcp" where method == "notifications/initialized":
            payload = ""
            status = 202
        case "/api/v1/mcp" where method == "tools/list":
            if paginatedTools, cursor == nil {
                let next = emptyContinuationCursor ? "" : "page-2"
                payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"tools":[{"name":"read_post"}],"nextCursor":"\#(next)"}}"#
            } else if paginatedTools, cursor == (emptyContinuationCursor ? "" : "page-2") {
                payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"tools":[{"name":"read_profile"}]}}"#
            } else {
                payload = #"{"jsonrpc":"2.0","id":"\#(id)","result":{"tools":[{"name":"read_post"}]}}"#
            }
            status = 200
        default:
            client?.urlProtocol(self, didFailWithError: URLError(.badURL))
            return
        }
        var headers = ["Content-Type": "application/json"]
        if sessionful, method == "initialize" { headers["Mcp-Session-Id"] = "fixture-session" }
        let response = HTTPURLResponse(
            url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: headers
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        if !payload.isEmpty { client?.urlProtocol(self, didLoad: Data(payload.utf8)) }
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        stopped = true
        deliveryLock.unlock()
    }

    private func finishClose() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped, let url = request.url else { return }
        let response = HTTPURLResponse(
            url: url, statusCode: 204, httpVersion: "HTTP/1.1", headerFields: [:]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocolDidFinishLoading(self)
    }

    private func finishHeld(payload: String) {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped, let url = request.url else { return }
        let response = HTTPURLResponse(
            url: url, statusCode: 200, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        if !payload.isEmpty { client?.urlProtocol(self, didLoad: Data(payload.utf8)) }
        client?.urlProtocolDidFinishLoading(self)
    }
}
