import Foundation
#if canImport(Security)
    import Security

    enum KeychainCookiePersistence {
        struct Backend {
            var copyMatching: (CFDictionary, UnsafeMutablePointer<CFTypeRef?>?) -> OSStatus
            var update: (CFDictionary, CFDictionary) -> OSStatus
            var add: (CFDictionary, UnsafeMutablePointer<CFTypeRef?>?) -> OSStatus
            var delete: (CFDictionary) -> OSStatus
        }

        static func load(
            serviceName: String,
            keychainKey: String,
            accessGroup: String?,
            backend: Backend = .security
        ) -> [HTTPCookie] {
            var query = baseQuery(
                serviceName: serviceName,
                keychainKey: keychainKey,
                accessGroup: accessGroup
            )
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var result: CFTypeRef?
            guard backend.copyMatching(query as CFDictionary, &result) == errSecSuccess,
                  let data = result as? Data,
                  let raw = try? PropertyListSerialization.propertyList(from: data, format: nil)
                  as? [[HTTPCookiePropertyKey: Any]]
            else { return [] }
            let now = Date()
            let survivors = raw.compactMap { properties -> (HTTPCookie, [HTTPCookiePropertyKey: Any])? in
                let normalized = KeychainCookieExpiration.propertiesByAddingAbsoluteExpiryIfNeeded(properties)
                guard !KeychainCookieExpiration.isExpired(
                    expiresDate: KeychainCookieExpiration.expiresDate(from: normalized),
                    properties: normalized,
                    now: now
                ) else { return nil }
                guard let cookie = HTTPCookie(properties: normalized) else { return nil }
                return (cookie, normalized)
            }
            let survivorCookies = survivors.map(\.0)
            let survivorProperties = survivors.map(\.1)
            if let normalizedData = propertyListData(from: survivorProperties),
               normalizedData != data {
                save(
                    survivorCookies,
                    serviceName: serviceName,
                    keychainKey: keychainKey,
                    accessGroup: accessGroup,
                    backend: backend
                )
            }
            return survivorCookies
        }

        static func save(
            _ cookies: [HTTPCookie],
            serviceName: String,
            keychainKey: String,
            accessGroup: String?,
            backend: Backend = .security
        ) {
            let props = cookies
                .compactMap(\.properties)
                .map(KeychainCookieExpiration.propertiesByAddingAbsoluteExpiryIfNeeded)
            guard let data = propertyListData(from: props) else { return }
            let query = baseQuery(
                serviceName: serviceName,
                keychainKey: keychainKey,
                accessGroup: accessGroup
            )
            let update: [CFString: Any] = [kSecValueData: data]
            let status = backend.update(query as CFDictionary, update as CFDictionary)
            if status == errSecItemNotFound {
                var addQuery = query
                addQuery[kSecValueData] = data
                addQuery[kSecAttrAccessible] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
                _ = backend.add(addQuery as CFDictionary, nil)
            }
        }

        static func delete(
            serviceName: String,
            keychainKey: String,
            accessGroup: String?,
            backend: Backend = .security
        ) {
            _ = backend.delete(
                baseQuery(
                    serviceName: serviceName,
                    keychainKey: keychainKey,
                    accessGroup: accessGroup
                ) as CFDictionary
            )
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
            if let group = accessGroup {
                query[kSecAttrAccessGroup] = group
            }
            return query
        }

        private static func propertyListData(from properties: [[HTTPCookiePropertyKey: Any]]) -> Data? {
            try? PropertyListSerialization.data(
                fromPropertyList: properties,
                format: .binary,
                options: 0
            )
        }
    }

    extension KeychainCookiePersistence.Backend {
        static let security = Self(
            copyMatching: SecItemCopyMatching,
            update: SecItemUpdate,
            add: SecItemAdd,
            delete: SecItemDelete
        )
    }
#endif
