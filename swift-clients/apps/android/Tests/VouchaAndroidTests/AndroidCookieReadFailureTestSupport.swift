import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

final class CookieReadFailureSecrets: AndroidEndpointSecretStoring, @unchecked Sendable {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var failsReads = false
    private var writes = 0

    func seed(_ value: String, forKey key: String) {
        lock.lock()
        defer { lock.unlock() }
        values[key] = value
    }

    func setReadFailure(_ fails: Bool) {
        lock.lock()
        defer { lock.unlock() }
        failsReads = fails
    }

    func snapshotValue(forKey key: String) -> String? {
        lock.lock()
        defer { lock.unlock() }
        return values[key]
    }

    func writeCount() -> Int {
        lock.lock()
        defer { lock.unlock() }
        return writes
    }

    func string(forKey key: String) throws -> String? {
        lock.lock()
        defer { lock.unlock() }
        if failsReads { throw URLError(.cannotOpenFile) }
        return values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        writes += 1
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values.removeValue(forKey: key)
    }
}

final class CookieReadRetryRequestProbe: @unchecked Sendable {
    private let lock = NSLock()
    private var storage: AndroidSessionCookieStorage?
    private var identity = Data()
    private var paths: [String] = []
    private var cookieValues: [[String]] = []

    func reset(storage: AndroidSessionCookieStorage?, identity: Data) {
        lock.lock()
        defer { lock.unlock() }
        self.storage = storage
        self.identity = identity
        paths = []
        cookieValues = []
    }

    func record(_ request: URLRequest) -> Data {
        lock.lock()
        let storage = storage
        let identity = identity
        lock.unlock()

        let path = request.url?.path ?? ""
        let values = request.url.flatMap { storage?.cookies(for: $0)?.map(\.value) } ?? []
        lock.lock()
        paths.append(path)
        cookieValues.append(values)
        lock.unlock()
        return identity
    }

    func requestPaths() -> [String] {
        lock.lock()
        defer { lock.unlock() }
        return paths
    }

    func cookieValuesAtRequests() -> [[String]] {
        lock.lock()
        defer { lock.unlock() }
        return cookieValues
    }
}

final class CookieReadRetryURLProtocol: URLProtocol {
    static let registry = CookieReadRetryProtocolRegistry()
    private let callbackLock = NSRecursiveLock()
    private var stopped = false

    // URLProtocol requires these class overrides for custom protocol dispatch.
    // swiftlint:disable:next static_over_final_class
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    // swiftlint:disable:next static_over_final_class
    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let host = request.url?.host,
              let probe = Self.registry.probe(forHost: host)
        else {
            deliver { $0.urlProtocol(self, didFailWithError: URLError(.unsupportedURL)) }
            return
        }
        let identity = probe.record(request)
        guard request.url?.path == "/api/v1/my/identity" else {
            deliver { $0.urlProtocol(self, didFailWithError: URLError(.unsupportedURL)) }
            return
        }
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: 200,
                  httpVersion: "HTTP/1.1",
                  headerFields: ["Content-Type": "application/json"]
              )
        else { return }
        deliver { $0.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed) }
        deliver { $0.urlProtocol(self, didLoad: identity) }
        deliver { $0.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        callbackLock.lock()
        stopped = true
        callbackLock.unlock()
    }

    private func deliver(_ callback: (URLProtocolClient) -> Void) {
        callbackLock.lock()
        defer { callbackLock.unlock() }
        guard !stopped, let client else { return }
        callback(client)
    }
}

final class CookieReadRetryProtocolRegistry: @unchecked Sendable {
    private let lock = NSLock()
    private var probes: [String: CookieReadRetryRequestProbe] = [:]

    func register(_ probe: CookieReadRetryRequestProbe, forHost host: String) {
        lock.lock()
        defer { lock.unlock() }
        probes[host.lowercased()] = probe
    }

    func unregister(forHost host: String) {
        lock.lock()
        defer { lock.unlock() }
        probes.removeValue(forKey: host.lowercased())
    }

    func probe(forHost host: String) -> CookieReadRetryRequestProbe? {
        lock.lock()
        defer { lock.unlock() }
        return probes[host.lowercased()]
    }
}

enum CookieReadFailureTestFixture {
    static func cookieSnapshot(value: String, domain: String = "voucha.ai") throws -> String {
        let record = try XCTUnwrap(
            AndroidSessionCookieStorage.CookieRecord(cookie: sessionCookie(value: value, domain: domain))
        )
        let data = try JSONEncoder().encode([record])
        return try XCTUnwrap(String(data: data, encoding: .utf8))
    }

    static func sessionCookie(value: String, domain: String = "voucha.ai") throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: value, .domain: domain, .path: "/", .secure: "TRUE"
        ]))
    }
}
