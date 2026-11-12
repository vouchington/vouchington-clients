import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

/// Canned native MCP mint transport. State is keyed by host so tests never share it, and every
/// access is serialized because `URLProtocol` callbacks arrive on transport queues.
final class SessionMCPMintProtocol: URLProtocol, @unchecked Sendable {
    struct Seen {
        let path: String
        let headers: [String: String]
        let body: Data
    }

    private final class State: @unchecked Sendable {
        let lock = NSLock()
        var seen: [Seen] = []
        var status = 200
        var resource = "https://api.test/api/v1/mcp"
        var tokenType = "Bearer"
        var expiresIn = 3_600
        var holding = false
        var held: [SessionMCPMintProtocol] = []
    }

    private nonisolated(unsafe) static var states: [String: State] = [:]
    private static let registry = NSLock()
    private var stopped = false

    static func reset(host: String) {
        registry.lock()
        defer { registry.unlock() }
        states[host] = State()
    }

    static func configure(
        host: String, status: Int = 200, resource: String? = nil, tokenType: String = "Bearer",
        expiresIn: Int = 3_600, holding: Bool = false
    ) {
        withState(host) { state in
            state.status = status
            state.resource = resource ?? state.resource
            state.tokenType = tokenType
            state.expiresIn = expiresIn
            state.holding = holding
        }
    }

    static func seen(host: String) -> [Seen] {
        withState(host) { $0.seen }
    }

    /// Completes every held mint with the configured response.
    static func releaseHeld(host: String) {
        let held = withState(host) { state -> [SessionMCPMintProtocol] in
            state.holding = false
            defer { state.held = [] }
            return state.held
        }
        for pending in held {
            pending.respond()
        }
    }

    private static func withState<T>(_ host: String, _ body: (State) -> T) -> T {
        registry.lock()
        let state = states[host] ?? State()
        states[host] = state
        registry.unlock()
        state.lock.lock()
        defer { state.lock.unlock() }
        return body(state)
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let host = request.url?.host ?? ""
        let body = Self.bodyData(request)
        let hold = Self.withState(host) { state -> Bool in
            state.seen.append(Seen(
                path: request.url?.path ?? "", headers: request.allHTTPHeaderFields ?? [:], body: body
            ))
            if state.holding { state.held.append(self) }
            return state.holding
        }
        if !hold { respond() }
    }

    override func stopLoading() {
        let host = request.url?.host ?? ""
        Self.withState(host) { state in
            stopped = true
            state.held.removeAll { $0 === self }
        }
    }

    private func respond() {
        let host = request.url?.host ?? ""
        let (status, json, isStopped) = Self.withState(host) { state -> (Int, String, Bool) in
            let count = state.seen.count
            let json = state.status == 200
                ? #"{"access_token":"token-\#(count)","expires_in":\#(state.expiresIn),"resource":"\#(state.resource)","#
                + #""scope":"mcp.user:read","token_type":"\#(state.tokenType)"}"#
                : #"{"error":"denied"}"#
            return (state.status, json, stopped)
        }
        guard !isStopped, let url = request.url else { return }
        let response = HTTPURLResponse(
            url: url, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(json.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    private static func bodyData(_ request: URLRequest) -> Data {
        if let body = request.httpBody { return body }
        guard let stream = request.httpBodyStream else { return Data() }
        stream.open()
        defer { stream.close() }
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 1_024)
        while stream.hasBytesAvailable {
            let count = stream.read(&buffer, maxLength: buffer.count)
            if count <= 0 { break }
            data.append(buffer, count: count)
        }
        return data
    }
}
