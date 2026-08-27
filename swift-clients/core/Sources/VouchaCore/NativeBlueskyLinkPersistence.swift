import Foundation
#if canImport(Security)
    import Security
#endif

protocol NativePendingStatePersisting: NativeOAuthSecureStatePersisting {}

struct UserDefaultsNativePendingState: NativePendingStatePersisting, @unchecked Sendable {
    let key: String
    let defaults: UserDefaults

    func read() -> Data? {
        defaults.data(forKey: key)
    }

    func write(_ data: Data) -> Bool {
        defaults.set(data, forKey: key)
        return true
    }

    func delete() throws {
        defaults.removeObject(forKey: key)
    }
}

#if canImport(Security)
    struct KeychainNativePendingState: NativePendingStatePersisting {
        let service: String
        let account: String

        func read() throws -> Data? {
            var query = baseQuery()
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var item: CFTypeRef?
            let status = SecItemCopyMatching(query as CFDictionary, &item)
            if status == errSecItemNotFound {
                return nil
            }
            guard status == errSecSuccess, let data = item as? Data else {
                throw NativeOAuthSecureStateError.unavailable
            }
            return data
        }

        func write(_ data: Data) -> Bool {
            (try? writeForOAuthCallbackClaim(data)) != nil
        }

        func writeForOAuthCallbackClaim(_ data: Data) throws {
            let query = baseQuery()
            let update: [CFString: Any] = [kSecValueData: data]
            let status = SecItemUpdate(query as CFDictionary, update as CFDictionary)
            if status == errSecSuccess {
                return
            }
            guard status == errSecItemNotFound else {
                throw NativeOAuthSecureStateError.unavailable
            }
            var addQuery = query
            addQuery[kSecValueData] = data
            addQuery[kSecAttrAccessible] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            guard SecItemAdd(addQuery as CFDictionary, nil) == errSecSuccess else {
                throw NativeOAuthSecureStateError.unavailable
            }
        }

        func delete() throws {
            let status = SecItemDelete(baseQuery() as CFDictionary)
            guard status == errSecSuccess || status == errSecItemNotFound else {
                throw NativeOAuthSecureStateError.unavailable
            }
        }

        private func baseQuery() -> [CFString: Any] {
            [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: service,
                kSecAttrAccount: account
            ]
        }
    }
#else
    final class KeychainNativePendingState: NativePendingStatePersisting, @unchecked Sendable {
        private let lock = NSLock()
        private var data: Data?

        init(service _: String, account _: String) {}

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
#endif

extension Data {
    func base64URLEncodedString() -> String {
        base64EncodedString()
            .replacingOccurrences(of: "+", with: "-")
            .replacingOccurrences(of: "/", with: "_")
            .replacingOccurrences(of: "=", with: "")
    }
}
