import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

final class CookieDiscardRecoverySecrets: AndroidEndpointSecretStoring, @unchecked Sendable {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var failsRemoval = false
    private var failsReads = false
    private var writes = 0
    private var removals = 0

    func seed(_ value: String, forKey key: String) {
        lock.lock()
        defer { lock.unlock() }
        values[key] = value
    }

    func setRemovalFailure(_ fails: Bool) {
        lock.lock()
        defer { lock.unlock() }
        failsRemoval = fails
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

    func counts() -> (writes: Int, removals: Int) {
        lock.lock()
        defer { lock.unlock() }
        return (writes, removals)
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
        if failsRemoval { throw URLError(.cannotWriteToFile) }
        removals += 1
        values.removeValue(forKey: key)
    }
}

final class CookieDiscardRequestProbe: @unchecked Sendable {
    private let lock = NSLock()
    private let secrets: CookieDiscardRecoverySecrets
    private let identity: Data
    private let snapshotKey: String
    private var snapshotValues: [String?] = []
    private var paths: [String] = []

    init(secrets: CookieDiscardRecoverySecrets, identity: Data, key: String) {
        self.secrets = secrets
        self.identity = identity
        snapshotKey = key
    }

    func record(_ request: URLRequest) -> Data {
        let snapshot = secrets.snapshotValue(forKey: snapshotKey)
        lock.lock()
        snapshotValues.append(snapshot)
        paths.append(request.url?.path ?? "")
        lock.unlock()
        return identity
    }

    func observations() -> (snapshots: [String?], paths: [String]) {
        lock.lock()
        defer { lock.unlock() }
        return (snapshotValues, paths)
    }
}

final class CookieDiscardURLProtocol: URLProtocol {
    static let registry = CookieDiscardProtocolRegistry()
    private let lock = NSRecursiveLock()
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
              let probe = Self.registry.probe(forHost: host),
              request.url?.path == "/api/v1/my/identity",
              let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: 200,
                  httpVersion: "HTTP/1.1",
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            deliver { $0.urlProtocol(self, didFailWithError: URLError(.unsupportedURL)) }
            return
        }
        let data = probe.record(request)
        deliver { $0.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed) }
        deliver { $0.urlProtocol(self, didLoad: data) }
        deliver { $0.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        lock.lock()
        stopped = true
        lock.unlock()
    }

    private func deliver(_ callback: (URLProtocolClient) -> Void) {
        lock.lock()
        defer { lock.unlock() }
        guard !stopped, let client else { return }
        callback(client)
    }
}

final class CookieDiscardProtocolRegistry: @unchecked Sendable {
    private let lock = NSLock()
    private var probes: [String: CookieDiscardRequestProbe] = [:]

    func register(_ probe: CookieDiscardRequestProbe, forHost host: String) {
        lock.lock()
        defer { lock.unlock() }
        probes[host.lowercased()] = probe
    }

    func unregister(forHost host: String) {
        lock.lock()
        defer { lock.unlock() }
        probes.removeValue(forKey: host.lowercased())
    }

    func probe(forHost host: String) -> CookieDiscardRequestProbe? {
        lock.lock()
        defer { lock.unlock() }
        return probes[host.lowercased()]
    }
}
