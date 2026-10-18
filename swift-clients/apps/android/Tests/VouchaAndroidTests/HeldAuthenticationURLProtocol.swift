import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import XCTest

final class HeldAuthenticationURLProtocol: URLProtocol {
    private final class WaitingRequest: @unchecked Sendable {
        let path: String
        let started: XCTestExpectation
        var protocolInstance: HeldAuthenticationURLProtocol?

        init(path: String, started: XCTestExpectation) {
            self.path = path
            self.started = started
        }
    }

    private static let registryLock = NSLock()
    private nonisolated(unsafe) static var requests: [String: WaitingRequest] = [:]

    private let callbackLock = NSRecursiveLock()
    private var stopped = false
    private var finished = false

    static func expectRequest(host: String, path: String) -> XCTestExpectation {
        let expectation = XCTestExpectation(description: "Held auth request to \(path)")
        registryLock.withLock {
            requests[host] = WaitingRequest(path: path, started: expectation)
        }
        return expectation
    }

    static func advance(host: String, to path: String, statusCode: Int, body: Data) -> XCTestExpectation {
        let expectation = XCTestExpectation(description: "Held auth request to \(path)")
        let instance = registryLock.withLock { () -> HeldAuthenticationURLProtocol? in
            let instance = requests[host]?.protocolInstance
            requests[host] = WaitingRequest(path: path, started: expectation)
            return instance
        }
        XCTAssertTrue(instance?.deliver(statusCode: statusCode, body: body) == true)
        return expectation
    }

    static func removeRequest(host: String) {
        _ = registryLock.withLock { requests.removeValue(forKey: host) }
    }

    @discardableResult
    static func respond(host: String, statusCode: Int, body: Data) -> Bool {
        let instance = registryLock.withLock { requests[host]?.protocolInstance }
        return instance?.deliver(statusCode: statusCode, body: body) ?? false
    }

    // URLProtocol requires class overrides for custom protocol dispatch.
    // swiftlint:disable:next static_over_final_class
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    // swiftlint:disable:next static_over_final_class
    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let host = request.url?.host else { return }
        let waiting = Self.registryLock.withLock { () -> WaitingRequest? in
            guard let waiting = Self.requests[host], request.url?.path == waiting.path else { return nil }
            waiting.protocolInstance = self
            return waiting
        }
        waiting?.started.fulfill()
    }

    override func stopLoading() {
        callbackLock.withLock { stopped = true }
    }

    private func deliver(statusCode: Int, body: Data) -> Bool {
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: statusCode,
                  httpVersion: "HTTP/1.1",
                  headerFields: ["Content-Type": "application/json"]
              )
        else { return false }

        return callbackLock.withLock {
            guard !stopped, !finished, let client else { return false }
            finished = true
            client.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            guard !stopped else { return false }
            client.urlProtocol(self, didLoad: body)
            guard !stopped else { return false }
            client.urlProtocolDidFinishLoading(self)
            return true
        }
    }
}
