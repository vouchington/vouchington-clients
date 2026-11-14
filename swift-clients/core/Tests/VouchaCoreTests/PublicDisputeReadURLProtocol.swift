import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif

/// Immutable canonical data and per-instance cancellation state avoid shared mutable handlers.
final class PublicDisputeReadURLProtocol: URLProtocol {
    private static let body = ApiFixtureLoader.data("native.moderation.disputes.detail.member.default")
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
        guard !stopped, let client, let url = request.url else { return }
        guard request.httpMethod == "GET",
              url.path == "/api/v1/disputes/00000000-0000-7000-8000-000000000201"
        else {
            client.urlProtocol(self, didFailWithError: URLError(.unsupportedURL))
            return
        }
        guard let response = HTTPURLResponse(
            url: url, statusCode: 200, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        ) else { return }
        client.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        guard !stopped else { return }
        client.urlProtocol(self, didLoad: Self.body)
        guard !stopped else { return }
        client.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        lock.lock()
        stopped = true
        lock.unlock()
    }
}
