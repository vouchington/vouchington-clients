import Foundation
import VouchaTestSupport

/// Serves the native mint route and the MCP resource together. All state is lock-protected.
final class SessionBoundMCPURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var mints = 0
    private static var rejectedBearers: Set<String> = []
    private static var requests: [String] = []
    private static var bearers: [String] = []
    private static var stallDeletes = false
    private static var mintCookies: [String] = []
    private let deliveryLock = NSLock()
    private var stopped = false

    static func reset(rejecting rejected: Set<String> = [], stallingDeletes: Bool = false) {
        lock.lock()
        defer { lock.unlock() }
        stallDeletes = stallingDeletes
        mints = 0
        rejectedBearers = rejected
        requests = []
        bearers = []
        mintCookies = []
    }

    /// `Cookie` header of each mint request; the fixture refuses a mint without both session cookies.
    static var mintCookieHeaders: [String] {
        lock.lock()
        defer { lock.unlock() }
        return mintCookies
    }

    static var paths: [String] {
        lock.lock()
        defer { lock.unlock() }
        return requests
    }

    static var mcpBearers: [String] {
        lock.lock()
        defer { lock.unlock() }
        return bearers
    }

    static var mintCount: Int {
        lock.lock()
        defer { lock.unlock() }
        return mints
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
        if request.httpMethod == "DELETE", Self.recordStalledDelete() { return }
        let rpc = URLRequestHTTPBody.data(from: request)
            .flatMap { (try? JSONSerialization.jsonObject(with: $0)) as? [String: Any] }
        let method = rpc?["method"] as? String
        let id = rpc?["id"] as? String ?? "1"
        let bearer = request.value(forHTTPHeaderField: "Authorization") ?? ""
        let (status, payload) = Self.answer(
            path: url.path, httpMethod: request.httpMethod ?? "", method: method, id: id, bearer: bearer,
            cookie: request.value(forHTTPHeaderField: "Cookie") ?? ""
        )
        var headers = ["Content-Type": "application/json"]
        if method == "initialize" { headers["Mcp-Session-Id"] = "fixture-session" }
        let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: headers)!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        if !payload.isEmpty { client?.urlProtocol(self, didLoad: Data(payload.utf8)) }
        client?.urlProtocolDidFinishLoading(self)
    }

    /// Records the DELETE and reports whether it must hang without a response.
    private static func recordStalledDelete() -> Bool {
        lock.lock()
        defer { lock.unlock() }
        guard stallDeletes else { return false }
        requests.append("DELETE /api/v1/mcp")
        return true
    }

    private static func answer(
        path: String, httpMethod: String, method: String?, id: String, bearer: String, cookie: String
    ) -> (Int, String) {
        lock.lock()
        defer { lock.unlock() }
        requests.append("\(httpMethod) \(path)")
        if path == "/api/v1/oauth/native-session-tokens" {
            mintCookies.append(cookie)
            guard cookie.contains("dt=device-secret"), cookie.contains("st=session-secret") else {
                return (401, #"{"error":"not authenticated"}"#)
            }
            mints += 1
            return (
                200,
                #"{"access_token":"token-\#(mints)","expires_in":3600,"#
                    + #""resource":"https://example.test/api/v1/mcp","scope":"mcp.user:read","token_type":"Bearer"}"#
            )
        }
        guard path == "/api/v1/mcp" else { return (404, "") }
        bearers.append(bearer)
        if httpMethod == "DELETE" { return (204, "") }
        if method == "notifications/initialized" { return (202, "") }
        if rejectedBearers.contains(bearer) { return (401, "") }
        let result = switch method {
        case "initialize": #"{"protocolVersion":"2025-06-18"}"#
        case "tools/call": #"{"content":[{"type":"text","text":"done"}],"isError":false}"#
        default: #"{"tools":[{"name":"server_tool"}]}"#
        }
        return (200, #"{"jsonrpc":"2.0","id":"\#(id)","result":\#(result)}"#)
    }

    override func stopLoading() {
        deliveryLock.lock()
        stopped = true
        deliveryLock.unlock()
    }
}
