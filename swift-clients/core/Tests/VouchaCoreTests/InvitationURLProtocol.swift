import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
import VouchaTestSupport

final class InvitationURLProtocol: URLProtocol, @unchecked Sendable {
    private static let lock = NSLock()
    private nonisolated(unsafe) static var responses: [String: (Data, Int)] = [:]
    private nonisolated(unsafe) static var requests: [(URL, String, String?)] = []

    static var requestHandlers: [String: (Data, Int)] {
        get { locked { responses } }
        set { locked { responses = newValue } }
    }

    private nonisolated(unsafe) static var responseURL: URL?

    static var finalResponseURL: URL? {
        get { locked { responseURL } }
        set { locked { responseURL = newValue } }
    }

    private nonisolated(unsafe) static var headers: [[String: String]] = []

    static var capturedHeaders: [[String: String]] {
        locked { headers }
    }

    static var capturedURLs: [URL] {
        locked { requests.map(\.0) }
    }

    static var capturedMethods: [String] {
        locked { requests.map(\.1) }
    }

    static var capturedBodies: [String?] {
        locked { requests.map(\.2) }
    }

    static func reset() {
        locked {
            responses = [:]
            requests = []
            headers = []
            responseURL = nil
        }
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let url = request.url else { return }
        let method = request.httpMethod ?? "GET"
        let body = URLRequestHTTPBody.utf8String(from: request)
        let (data, status, finalURL) = Self.locked {
            Self.headers.append(request.allHTTPHeaderFields ?? [:])
            Self.requests.append((url, method, body))
            let (data, status) = Self.responses["\(method) \(url.path)"] ?? (Data("{}".utf8), 500)
            return (data, status, Self.responseURL ?? url)
        }
        let response = HTTPURLResponse(
            url: finalURL, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private static func locked<T>(_ action: () -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return action()
    }
}
