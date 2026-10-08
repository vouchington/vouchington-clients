#if canImport(Security)
    import Foundation
    import Security

    /// Per-member OAuth credentials, confined to this device and never synchronized to iCloud.
    public actor KeychainMemberMCPOAuthTokenStore: MemberMCPOAuthTokenStore {
        public enum Failure: Error { case keychain(OSStatus) }

        private let accessGroup: String?
        private let service = "ai.voucha.member-mcp-oauth"

        public init(accessGroup: String? = nil) {
            self.accessGroup = accessGroup
        }

        public func load(accountId: String) throws -> MemberMCPOAuthTokens? {
            var query = baseQuery(accountId: accountId)
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var item: CFTypeRef?
            let status = SecItemCopyMatching(query as CFDictionary, &item)
            if status == errSecItemNotFound { return nil }
            guard status == errSecSuccess else { throw Failure.keychain(status) }
            guard let data = item as? Data else { throw Failure.keychain(errSecDecode) }
            return try JSONDecoder().decode(MemberMCPOAuthTokens.self, from: data)
        }

        public func save(_ tokens: MemberMCPOAuthTokens, accountId: String) throws {
            let data = try JSONEncoder().encode(tokens)
            let query = baseQuery(accountId: accountId)
            let update = SecItemUpdate(query as CFDictionary, [kSecValueData: data] as CFDictionary)
            if update == errSecSuccess { return }
            guard update == errSecItemNotFound else { throw Failure.keychain(update) }
            var add = query
            add[kSecValueData] = data
            add[kSecAttrAccessible] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            add[kSecAttrSynchronizable] = kCFBooleanFalse
            let result = SecItemAdd(add as CFDictionary, nil)
            if result == errSecSuccess { return }
            if result == errSecDuplicateItem,
               SecItemUpdate(query as CFDictionary, [kSecValueData: data] as CFDictionary) == errSecSuccess {
                return
            }
            throw Failure.keychain(result)
        }

        public func clear(accountId: String) throws {
            let status = SecItemDelete(baseQuery(accountId: accountId) as CFDictionary)
            guard status == errSecSuccess || status == errSecItemNotFound else {
                throw Failure.keychain(status)
            }
        }

        private func baseQuery(accountId: String) -> [CFString: Any] {
            var query: [CFString: Any] = [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: service,
                kSecAttrAccount: accountId
            ]
            if let accessGroup { query[kSecAttrAccessGroup] = accessGroup }
            return query
        }
    }
#endif
