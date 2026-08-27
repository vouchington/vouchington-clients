import Foundation
#if canImport(Security)
    import Security

    /// Persists the App Attest key ID in the Keychain so it survives app relaunches. Apple only
    /// allows one attested key per app install; losing this ID orphans the attested key and
    /// forces a fresh (server-rejected, until re-attested) key on next launch.
    ///
    /// Mirrors `KeychainCookieStorage`'s in-memory-cache-backed-by-Keychain design: reads within
    /// a process hit the cache, not the Keychain daemon, so a slow or momentarily unavailable
    /// Keychain can't stall/fail `ensureAttestedKey()`'s hot path.
    public final class AppAttestKeyStore: @unchecked Sendable {
        private let serviceName: String
        private let keychainKey: String
        private let accessGroup: String?
        private let queue = DispatchQueue(label: "ai.voucha.AppAttestKeyStore")
        private var cachedKeyId: String?

        public init(serviceName: String = "ai.voucha", accessGroup: String? = nil) {
            self.serviceName = serviceName
            keychainKey = "\(serviceName).app-attest-key-id"
            self.accessGroup = accessGroup
            cachedKeyId = Self.loadFromKeychain(
                query: Self.baseQuery(serviceName: serviceName, keychainKey: keychainKey, accessGroup: accessGroup)
            )
        }

        public func loadKeyId() -> String? {
            queue.sync {
                if cachedKeyId == nil {
                    // Another AppAttestKeyStore instance (e.g. in a CAPTCHA-gated view model) may
                    // have written a key to Keychain after this instance was initialized. Re-read
                    // once on nil so the shared Keychain slot is always authoritative.
                    cachedKeyId = Self.loadFromKeychain(query: baseQuery())
                }
                return cachedKeyId
            }
        }

        public func save(keyId: String) {
            queue.sync {
                cachedKeyId = keyId
                Self.persistToKeychain(keyId, query: baseQuery())
            }
        }

        public func clear() {
            queue.sync {
                cachedKeyId = nil
                SecItemDelete(baseQuery() as CFDictionary)
            }
        }

        private func baseQuery() -> [CFString: Any] {
            Self.baseQuery(serviceName: serviceName, keychainKey: keychainKey, accessGroup: accessGroup)
        }

        private static func baseQuery(
            serviceName: String,
            keychainKey: String,
            accessGroup: String?
        ) -> [CFString: Any] {
            var query: [CFString: Any] = [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: serviceName,
                kSecAttrAccount: keychainKey
            ]
            if let accessGroup {
                query[kSecAttrAccessGroup] = accessGroup
            }
            return query
        }

        private static func loadFromKeychain(query: [CFString: Any]) -> String? {
            var query = query
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var result: CFTypeRef?
            guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess,
                  let data = result as? Data
            else { return nil }
            return String(data: data, encoding: .utf8)
        }

        private static func persistToKeychain(_ keyId: String, query: [CFString: Any]) {
            guard let data = keyId.data(using: .utf8) else { return }
            let update: [CFString: Any] = [kSecValueData: data]
            let status = SecItemUpdate(query as CFDictionary, update as CFDictionary)
            if status == errSecItemNotFound {
                var addQuery = query
                addQuery[kSecValueData] = data
                addQuery[kSecAttrAccessible] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
                SecItemAdd(addQuery as CFDictionary, nil)
            }
        }
    }
#else
    /// Non-Darwin fallback (Linux CI, non-Darwin test hosts) where `Security`/Keychain are
    /// unavailable. App Attest itself never runs on these platforms — `AppAttestationService`
    /// only calls `DeviceCheckAppAttestProvider` under `#if canImport(DeviceCheck)` — so this
    /// exists solely to keep `AppAttestKeyStore` compiling wherever `VouchaAuth` builds.
    public final class AppAttestKeyStore: @unchecked Sendable {
        private let queue = DispatchQueue(label: "ai.voucha.AppAttestKeyStore")
        private var cachedKeyId: String?

        public init(serviceName _: String = "ai.voucha", accessGroup _: String? = nil) {}

        public func loadKeyId() -> String? {
            queue.sync { cachedKeyId }
        }

        public func save(keyId: String) {
            queue.sync { cachedKeyId = keyId }
        }

        public func clear() {
            queue.sync { cachedKeyId = nil }
        }
    }
#endif
