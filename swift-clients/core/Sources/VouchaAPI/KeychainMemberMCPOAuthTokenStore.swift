#if canImport(Security)
    import Foundation
    import Security

    /// Per-member OAuth credentials, confined to this device and never synchronized to iCloud.
    public actor KeychainMemberMCPOAuthTokenStore: MemberMCPOAuthTokenStore, MemberMCPOAuthAccountClearing {
        public enum Failure: Error { case keychain(OSStatus) }

        private let accessGroup: String?
        private let service = "ai.voucha.member-mcp-oauth"

        public init(accessGroup: String? = nil) {
            self.accessGroup = accessGroup
        }

        public func load(scope: MemberMCPOAuthTokenScope) throws -> MemberMCPOAuthTokens? {
            var query = baseQuery(scope: scope)
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var item: CFTypeRef?
            let status = SecItemCopyMatching(query as CFDictionary, &item)
            if status == errSecItemNotFound { return nil }
            guard status == errSecSuccess else { throw Failure.keychain(status) }
            guard let data = item as? Data else { throw Failure.keychain(errSecDecode) }
            return try JSONDecoder().decode(MemberMCPOAuthTokens.self, from: data)
        }

        public func save(_ tokens: MemberMCPOAuthTokens, scope: MemberMCPOAuthTokenScope) throws {
            let data = try JSONEncoder().encode(tokens)
            let query = baseQuery(scope: scope)
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

        public func clear(scope: MemberMCPOAuthTokenScope) throws {
            let status = SecItemDelete(baseQuery(scope: scope) as CFDictionary)
            guard status == errSecSuccess || status == errSecItemNotFound else {
                throw Failure.keychain(status)
            }
        }

        public func clearAccount(_ accountId: String) throws {
            var query = serviceQuery()
            query[kSecReturnAttributes] = true
            query[kSecMatchLimit] = kSecMatchLimitAll
            var result: CFTypeRef?
            let status = SecItemCopyMatching(query as CFDictionary, &result)
            if status == errSecItemNotFound { return }
            guard status == errSecSuccess, let records = result as? [[String: Any]] else {
                throw Failure.keychain(status == errSecSuccess ? errSecDecode : status)
            }
            let accountPrefix = Self.accountPrefix(for: accountId)
            for record in records {
                guard let key = record[kSecAttrAccount as String] as? String,
                      key.hasPrefix(accountPrefix) else { continue }
                var deletion = serviceQuery()
                deletion[kSecAttrAccount] = key
                let deleted = SecItemDelete(deletion as CFDictionary)
                guard deleted == errSecSuccess || deleted == errSecItemNotFound else {
                    throw Failure.keychain(deleted)
                }
            }
        }

        static func accountKey(for scope: MemberMCPOAuthTokenScope) -> String {
            let parts = [
                scope.issuerIdentifier,
                scope.resource.absoluteString,
                scope.clientId.absoluteString
            ]
            return accountPrefix(for: scope.accountId) + parts.map { "\($0.utf8.count):\($0)" }.joined()
        }

        static func accountPrefix(for accountId: String) -> String {
            "\(accountId.utf8.count):\(accountId)"
        }

        private func baseQuery(scope: MemberMCPOAuthTokenScope) -> [CFString: Any] {
            var query = serviceQuery()
            query[kSecAttrAccount] = Self.accountKey(for: scope)
            return query
        }

        private func serviceQuery() -> [CFString: Any] {
            var query: [CFString: Any] = [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: service
            ]
            #if os(macOS)
                query[kSecUseDataProtectionKeychain] = true
            #endif
            if let accessGroup { query[kSecAttrAccessGroup] = accessGroup }
            return query
        }
    }
#endif
