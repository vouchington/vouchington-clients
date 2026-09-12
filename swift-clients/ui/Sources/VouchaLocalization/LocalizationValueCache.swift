import Foundation

/// Byte-bounded overlay of live catalog values. Bundled resources remain the
/// structural fallback; expired entries still serve until a refresh succeeds.
public final class LocalizationValueCache: @unchecked Sendable {
    public static let defaultMaxBytes = 524_288
    public static let shared = LocalizationValueCache()

    private let lock = NSLock()
    private let maxBytes: Int
    private var entries: [String: Entry] = [:]
    private var order: [String] = []

    public init(maxBytes: Int = LocalizationValueCache.defaultMaxBytes) {
        self.maxBytes = maxBytes
    }

    public func reset() {
        lock.lock()
        defer { lock.unlock() }
        entries.removeAll()
        order.removeAll()
    }

    public func value(for key: String, locale: String) -> String? {
        lock.lock()
        defer { lock.unlock() }
        guard let entry = entries[locale] else { return nil }
        touchLocked(locale)
        return entry.values[key]
    }

    public func etag(for locale: String) -> String? {
        lock.lock()
        defer { lock.unlock() }
        guard let revision = entries[locale]?.revision else { return nil }
        return "\"\(revision)\""
    }

    public func isExpired(locale: String, now: Date = Date()) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        guard let entry = entries[locale] else { return true }
        return now >= entry.expiresAt
    }

    public func apply(
        locale: String,
        revision: String,
        ttlSeconds: Int,
        values: [String: String],
        now: Date = Date()
    ) {
        lock.lock()
        defer { lock.unlock() }
        var merged = entries[locale]?.values ?? [:]
        for (key, value) in values {
            merged[key] = value
        }
        let byteCount = merged.values.reduce(0) { $0 + $1.utf8.count }
        entries[locale] = Entry(
            revision: revision,
            expiresAt: now.addingTimeInterval(TimeInterval(ttlSeconds)),
            values: merged,
            byteCount: byteCount
        )
        touchLocked(locale)
        evictLocked()
    }

    public func rememberNotModified(locale: String, ttlSeconds: Int, now: Date = Date()) {
        lock.lock()
        defer { lock.unlock() }
        guard var entry = entries[locale] else { return }
        entry.expiresAt = now.addingTimeInterval(TimeInterval(ttlSeconds))
        entries[locale] = entry
        touchLocked(locale)
    }

    private func touchLocked(_ locale: String) {
        order.removeAll { $0 == locale }
        order.append(locale)
    }

    private func evictLocked() {
        var total = entries.values.reduce(0) { $0 + $1.byteCount }
        while total > maxBytes, order.count > 1, let oldest = order.first {
            order.removeFirst()
            if let removed = entries.removeValue(forKey: oldest) {
                total -= removed.byteCount
            }
        }
    }

    private struct Entry {
        var revision: String
        var expiresAt: Date
        var values: [String: String]
        var byteCount: Int
    }
}
