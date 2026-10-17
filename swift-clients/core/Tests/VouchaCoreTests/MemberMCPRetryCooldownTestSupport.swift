import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaTestSupport

final class RetryCooldownClock: @unchecked Sendable {
    private let lock = NSLock()
    private var seconds: TimeInterval = 0
    private var wall = Date(timeIntervalSince1970: 1_792_195_200)

    func uptime() -> TimeInterval {
        lock.lock()
        defer { lock.unlock() }
        return seconds
    }

    func date() -> Date {
        lock.lock()
        defer { lock.unlock() }
        return wall
    }

    func advance(_ value: TimeInterval) {
        lock.lock()
        defer { lock.unlock() }
        seconds += value
    }

    func jumpWallClock() {
        lock.lock()
        defer { lock.unlock() }
        wall = wall.addingTimeInterval(365 * 24 * 60 * 60)
    }
}

final class RetryCooldownURLProtocol: URLProtocol {
    private struct State {
        var mode = "none"
        var method = "tools/list"
        var header: String?
        var failed = false
        var requests = 0
        var toolCalls = 0
        var deletes = 0
        var replies = 0
        var held: [RetryCooldownURLProtocol] = []
        var onHeld: ((Int) -> Void)?
    }

    private static let lock = NSLock()
    private static var state = State()

    static func configure(
        mode: String, method: String, header: String? = nil, onHeld: ((Int) -> Void)? = nil
    ) {
        lock.lock()
        defer { lock.unlock() }
        state = State(mode: mode, method: method, header: header, onHeld: onHeld)
    }

    static func requests() -> Int {
        snapshot().requests
    }

    static func toolCalls() -> Int {
        snapshot().toolCalls
    }

    static func replies() -> Int {
        snapshot().replies
    }

    static func deletes() -> Int {
        snapshot().deletes
    }

    private static func snapshot() -> State {
        lock.lock()
        defer { lock.unlock() }
        return state
    }

    static func release(_ index: Int, seconds: Int) {
        let held = snapshot().held[index]
        held.deliver(status: 429, headers: ["Retry-After": String(seconds)], data: Data())
    }

    static func releasePing(_ index: Int) {
        let held = snapshot().held[index]
        let ping = #"{"jsonrpc":"2.0","id":"server-ping","method":"ping"}"#
        held.deliver(status: 200, headers: ["Content-Type": "text/event-stream"], data: Data("data: \(ping)\n\n".utf8))
    }

    static func stopHolding() {
        lock.lock()
        defer { lock.unlock() }
        state.mode = "none"
    }

    private let deliveryLock = NSRecursiveLock()
    private var stopped = false

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let body = URLRequestHTTPBody.data(from: request)
        let rpc = body.flatMap { try? JSONSerialization.jsonObject(with: $0) as? [String: Any] } ?? [:]
        let method = rpc["method"] as? String
        Self.lock.lock()
        Self.state.requests += 1
        if method == "tools/call" { Self.state.toolCalls += 1 }
        if request.httpMethod == "DELETE" { Self.state.deletes += 1 }
        if Self.state.mode == "held", method == "tools/list" {
            Self.state.held.append(self)
            let count = Self.state.held.count
            let onHeld = Self.state.onHeld
            Self.lock.unlock()
            onHeld?(count)
            return
        }
        let serverReply = method == nil && rpc["id"] as? String == "server-ping"
        if serverReply { Self.state.replies += 1 }
        let ping = Self.state.mode == "server-reply" && method == "tools/list" && !Self.state.failed
        let limited = Self.state.mode == "server-reply"
            ? serverReply && !Self.state.failed
            : Self.state.mode != "none" && !Self.state.failed && method == Self.state.method
        if limited { Self.state.failed = true }
        let state = Self.state
        Self.lock.unlock()
        var status = request.httpMethod == "DELETE" ? 204 : 200
        var headers = ["Content-Type": "application/json", "Mcp-Session-Id": "retry-session"]
        var result: [String: Any] = ["tools": [], "content": [], "isError": false]
        if method == "initialize" { result = ["protocolVersion": "2025-06-18"] }
        if method == "notifications/initialized" { status = 202 }
        if ping {
            let result = #"{"jsonrpc":"2.0","id":"server-ping","method":"ping"}"#
            deliver(status: 200, headers: ["Content-Type": "text/event-stream"], data: Data("data: \(result)\n\n".utf8))
            return
        }
        if limited {
            if state.mode == "in-band" {
                result = ["isError": true, "content": [[
                    "type": "text", "text": #"{"error":{"code":"RATE_LIMIT","retryAfterSeconds":17}}"#
                ]]]
            } else {
                status = 429
                if state.mode == "seconds" || state.mode == "server-reply" { headers["Retry-After"] = "17" }
                else if state.mode == "date" { headers["Retry-After"] = "Sat, 17 Oct 2026 00:00:17 GMT" }
                else { headers["Retry-After"] = state.header }
            }
        }
        let envelope: [String: Any] = ["jsonrpc": "2.0", "id": rpc["id"] ?? "none", "result": result]
        let data = (try? JSONSerialization.data(withJSONObject: envelope)) ?? Data()
        deliver(status: status, headers: headers, data: data)
    }

    private func deliver(status: Int, headers: [String: String], data: Data) {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped else { return }
        let response = HTTPURLResponse(url: request.url!, statusCode: status, httpVersion: nil, headerFields: headers)!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        stopped = true
    }
}
