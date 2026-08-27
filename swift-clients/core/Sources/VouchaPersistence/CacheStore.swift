import Foundation

/// Protocol for a read-through cache. Intentionally generic so non-Apple
/// implementations (Android) can substitute their own backing store.
public protocol CacheStore: Sendable {
    func get<T: Codable & Sendable>(key: String, type: T.Type) async -> T?
    func set(key: String, value: some Codable & Sendable, ttl: TimeInterval?) async
    func invalidate(key: String) async
    func invalidateAll() async
}
