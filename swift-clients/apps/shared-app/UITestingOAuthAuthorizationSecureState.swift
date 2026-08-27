#if DEBUG
    import Foundation
    import VouchaCore
    import VouchaPersistence

    final class UITestingOAuthAuthorizationSecureState:
        NativeOAuthSecureStatePersisting,
        @unchecked Sendable {
        private let lock = NSLock()
        private var data: Data?

        func read() -> Data? {
            lock.withLock { data }
        }

        func write(_ data: Data) -> Bool {
            lock.withLock { self.data = data }
            return true
        }

        func delete() throws {
            lock.withLock { data = nil }
        }
    }

    final class UITestingFeatureFlagOverrideStore:
        FeatureFlagOverridePersisting,
        @unchecked Sendable {
        private let lock = NSLock()
        private var values: [String: Bool] = [:]

        func load() -> [String: Bool] {
            lock.withLock { values }
        }

        func set(_ value: Bool, for key: String) {
            lock.withLock { values[key] = value }
        }

        func remove(_ key: String) {
            lock.withLock { _ = values.removeValue(forKey: key) }
        }

        func clear() {
            lock.withLock { values.removeAll() }
        }
    }
#endif
