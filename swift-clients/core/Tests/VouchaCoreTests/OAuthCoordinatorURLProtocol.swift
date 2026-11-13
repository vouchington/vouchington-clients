import Foundation

final class OAuthCoordinatorURLProtocol: URLProtocol {
    static let lock = NSLock()
    static var responses: [String: [(Data, Int)]] = [:]
    static var requests: [URLRequest] = []
    /// When set, responses for this path wait for the gate after the request is recorded.
    static var heldPath: String?
    static var holdGate: DispatchSemaphore?

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        let response = Self.lock.withLock { () -> (Data, Int) in
            Self.requests.append(request)
            guard var queued = Self.responses[path], !queued.isEmpty else {
                return (
                    path == "/api/v1/session" ? ApiFixtureLoader
                        .data("native.auth.session.refresh.rotated") : Data("{}".utf8),
                    200
                )
            }
            let next = queued.removeFirst()
            Self.responses[path] = queued
            return next
        }
        guard let gate = Self.lock.withLock({ Self.heldPath == path ? Self.holdGate : nil }) else {
            deliver(response)
            return
        }
        // Release held responses off the loading thread so cancellation can still reach stopLoading().
        nonisolated(unsafe) let held = self
        DispatchQueue.global().async {
            gate.wait()
            held.deliver(response)
        }
    }

    private let deliveryLock = NSLock()
    private var stopped = false

    private func deliver(_ response: (Data, Int)) {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        guard !stopped else { return }
        let http = HTTPURLResponse(
            url: request.url!,
            statusCode: response.1,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: http, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: response.0)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {
        deliveryLock.lock()
        defer { deliveryLock.unlock() }
        stopped = true
    }
}
