import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

enum SessionRefreshReply {
    case http(Int, Data)
    case failure(URLError.Code)
}

/// The lock protects response consumption and request snapshots as one boundary.
final class SessionRefreshTransport: @unchecked Sendable {
    private let lock = NSLock()
    private var responses: [SessionRefreshReply]
    private var paths: [String] = []

    init(responses: [SessionRefreshReply]) {
        self.responses = responses
    }

    func response(for request: URLRequest) -> SessionRefreshReply {
        lock.lock()
        defer { lock.unlock() }
        paths.append(request.url?.path ?? "")
        guard request.url?.path == "/api/v1/my/identity", !responses.isEmpty else {
            return .failure(.unsupportedURL)
        }
        return responses.removeFirst()
    }

    func requestPaths() -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return paths
    }
}

final class SessionRefreshURLProtocol: URLProtocol {
    private static let registry = SessionRefreshRegistry()
    private let lock = NSRecursiveLock()
    private var stopped = false

    static func register(_ transport: SessionRefreshTransport, host: String) {
        registry.set(transport, host: host)
    }

    static func unregister(host: String) {
        registry.set(nil, host: host)
    }

    // URLProtocol dispatch requires class overrides.
    // swiftlint:disable:next static_over_final_class
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    // swiftlint:disable:next static_over_final_class
    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        lock.lock()
        defer { lock.unlock() }
        guard !stopped, let client, let url = request.url else { return }
        let reply = Self.registry.get(host: url.host ?? "")?.response(for: request)
            ?? .failure(.unsupportedURL)
        switch reply {
        case let .failure(code):
            client.urlProtocol(self, didFailWithError: URLError(code))
        case let .http(status, data):
            guard let response = HTTPURLResponse(
                url: url, statusCode: status, httpVersion: "HTTP/1.1",
                headerFields: ["Content-Type": "application/json"]
            ) else { return }
            client.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            guard !stopped else { return }
            client.urlProtocol(self, didLoad: data)
            guard !stopped else { return }
            client.urlProtocolDidFinishLoading(self)
        }
    }

    override func stopLoading() {
        lock.lock()
        stopped = true
        lock.unlock()
    }
}

/// Each test uses a unique host; the registry never exposes mutable shared storage.
private final class SessionRefreshRegistry: @unchecked Sendable {
    private let lock = NSLock()
    private var transports: [String: SessionRefreshTransport] = [:]

    func set(_ value: SessionRefreshTransport?, host: String) {
        lock.lock()
        defer { lock.unlock() }
        transports[host] = value
    }

    func get(host: String) -> SessionRefreshTransport? {
        lock.lock()
        defer { lock.unlock() }
        return transports[host]
    }
}
