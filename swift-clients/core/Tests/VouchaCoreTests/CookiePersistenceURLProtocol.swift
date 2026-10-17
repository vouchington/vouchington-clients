import Foundation

/// Immutable route responses avoid shared mutable protocol state across concurrent tests.
final class CookiePersistenceURLProtocol: URLProtocol {
    private let lock = NSRecursiveLock()
    private var stopped = false

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        lock.lock()
        defer { lock.unlock() }
        guard !stopped, let url = request.url else { return }
        let path = url.path
        if path == "/network-error" {
            client?.urlProtocol(self, didFailWithError: URLError(.networkConnectionLost))
            return
        }
        let status = path.hasPrefix("/bootstrap-error") || path == "/error" ? 500 : path == "/not-modified" ? 304 : 200
        let body = switch path {
        case "/api/v1/session":
            #"{"session":{"dt":"device-token","st":"session-token","dte":3600,"ste":3600,"secure":false}}"#
        case "/raw": "raw bytes"
        case "/lines": "first\nsecond\n"
        default: "{}"
        }
        guard let response = HTTPURLResponse(
            url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: ["Content-Type": "application/json"]
        ) else { return }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        guard !stopped else { return }
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        guard !stopped, path != "/hold" else { return }
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        lock.lock()
        stopped = true
        lock.unlock()
    }
}
