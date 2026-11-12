import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
import VouchaTestSupport
import XCTest

final class SessionRecoveryTransport: @unchecked Sendable {
    struct Reply {
        let status: Int
        let body: Data
        var headers: [String: String] = [:]
    }

    private let lock = NSLock()
    private var requests: [URLRequest] = []
    var handler: ((URLRequest, Int) -> Reply?)?
    private var held: SessionRecoveryProtocol?

    var captured: [URLRequest] {
        lock.withLock { requests }
    }

    func receive(_ request: URLRequest, transport: SessionRecoveryProtocol) {
        var captured = request
        captured.httpBody = URLRequestHTTPBody.data(from: request)
        let count = lock.withLock {
            requests.append(captured)
            return requests.count
        }
        if let reply = handler?(request, count) { transport.deliver(reply) }
        else { lock.withLock { held = transport } }
    }

    func release(_ reply: Reply) {
        let transport = lock.withLock { let current = held
            held = nil
            return current
        }
        transport?.deliver(reply)
    }

    static func tokens(_ token: String = "fresh", uid: String? = "user") -> Reply {
        var session: [String: Any] = [
            "dt": "d-\(token)", "st": "s-\(token)", "dte": 3_600, "ste": 900, "secure": false,
            "did": "device", "sid": "session", "session": [
                "client": "web", "did": "device", "sid": "session", "rca": 1, "sca": 1, "rol": [],
                "uid": uid as Any? ?? NSNull()
            ]
        ]
        if let uid { session["uid"] = uid }
        let copy = ["session": session]
        return Reply(status: 200, body: try! JSONSerialization.data(withJSONObject: copy))
    }

    static let empty = Reply(status: 200, body: Data("{}".utf8))
    static let unauthorized = Reply(status: 401, body: Data("{\"code\":\"AUTH_REQUIRED\"}".utf8))
}

final class SessionRecoveryProtocol: URLProtocol {
    private static let registryLock = NSLock()
    private static var registry: [String: SessionRecoveryTransport] = [:]
    private let lock = NSRecursiveLock()
    private var stopped = false

    static func register(_ transport: SessionRecoveryTransport, host: String) {
        registryLock.withLock { registry[host] = transport }
    }

    static func remove(_ host: String) {
        _ = registryLock.withLock { registry.removeValue(forKey: host) }
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let transport = Self.registryLock.withLock { Self.registry[request.url!.host!] }
        transport?.receive(request, transport: self)
    }

    override func stopLoading() {
        lock.withLock { stopped = true }
    }

    func deliver(_ reply: SessionRecoveryTransport.Reply) {
        lock.withLock {
            guard !stopped else { return }
            let response = HTTPURLResponse(
                url: request.url!,
                statusCode: reply.status,
                httpVersion: "HTTP/1.1",
                headerFields: reply.headers
            )!
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: reply.body)
            client?.urlProtocolDidFinishLoading(self)
        }
    }
}
