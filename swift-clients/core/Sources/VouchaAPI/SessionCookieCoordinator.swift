import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

/// Cookie stores with durable snapshots commit the entire snapshot before publishing it in memory.
public protocol TransactionalSessionCookieStorage: AnyObject {
    func replaceCookies(_ cookies: [HTTPCookie]) throws
}

/// Shared by every first-party API client using the same jar. Transport cookie handling is disabled.
final class SessionCookieCoordinator: @unchecked Sendable {
    struct Stamp: Equatable {
        let epoch: UInt64
        let revision: UInt64
    }

    private static let registryLock = NSLock()
    private final class WeakEntry {
        weak var value: SessionCookieCoordinator?
        init(_ value: SessionCookieCoordinator) {
            self.value = value
        }
    }

    private static var registry: [ObjectIdentifier: WeakEntry] = [:]
    let storage: HTTPCookieStorage
    let lock = NSRecursiveLock()
    var signingOut = false
    var epoch: UInt64 = 0
    var revision: UInt64 = 0
    var confirmedIdentityRevision: UInt64?
    var invalidationObservers: [UUID: @Sendable (UInt64, UInt64) -> Void] = [:]
    var rotation: (UUID, Task<SessionRotationResult, Error>)?

    static func shared(for storage: HTTPCookieStorage) -> SessionCookieCoordinator {
        registryLock.withLock {
            registry = registry.filter { $0.value.value != nil }
            let key = ObjectIdentifier(storage)
            if let existing = registry[key]?.value { return existing }
            let created = SessionCookieCoordinator(storage: storage)
            registry[key] = WeakEntry(created)
            return created
        }
    }

    private init(storage: HTTPCookieStorage) {
        self.storage = storage
    }

    var hasConfirmedIdentity: Bool {
        lock.withLock { confirmedIdentityRevision == revision }
    }

    var stamp: Stamp {
        lock.withLock { Stamp(epoch: epoch, revision: revision) }
    }

    func beginSignOut() {
        lock.withLock { signingOut = true
            epoch &+= 1
            confirmedIdentityRevision = nil
            rotation = nil
        }
    }

    func endSignOut() {
        lock.withLock { signingOut = false }
    }

    func tokens(for origin: URL, stamp: Stamp) throws -> [String: String] {
        try lock.withLock {
            try check(stamp)
            return Dictionary(
                (storage.cookies(for: origin) ?? []).filter { ["dt", "st"].contains($0.name) }
                    .map { ($0.name, $0.value) },
                uniquingKeysWith: { _, last in last }
            )
        }
    }

    func invalidate() {
        lock.withLock {
            epoch &+= 1
            confirmedIdentityRevision = nil
            rotation = nil
        }
    }

    func check(_ stamp: Stamp) throws {
        try lock.withLock {
            guard epoch == stamp.epoch else { throw CancellationError() }
        }
    }

    func prepare(_ request: URLRequest, origin: URL) throws -> (URLRequest, Stamp) {
        try lock.withLock {
            guard !signingOut || request.url?.path == "/api/v1/auth/logout" else { throw CancellationError() }
            var request = request
            if let url = request.url, url.scheme == origin.scheme, url.host == origin.host,
               url.port == origin.port {
                let cookies = storage.cookies(for: url) ?? []
                request.setValue(HTTPCookie.requestHeaderFields(with: cookies)["Cookie"], forHTTPHeaderField: "Cookie")
            }
            request.httpShouldHandleCookies = false
            return (request, Stamp(epoch: epoch, revision: revision))
        }
    }

}

struct SessionRotationResult: @unchecked Sendable {
    let data: Data
    let response: URLResponse
}
