import Foundation
#if canImport(Security)
    import Security
#endif

#if canImport(Security)
    public enum LocalLLMSecretStoreError: Error, Equatable {
        case keychainOperationFailed
    }

    public struct KeychainLocalLLMSecretStore: LocalLLMSecretStoring {
        private let service = "ai.voucha.local-llm"

        public init() {}

        public func readAPIKey(for endpointID: UUID) async -> String? {
            var query = baseQuery()
            query[kSecAttrAccount] = account(for: endpointID)
            query[kSecReturnData] = true
            query[kSecMatchLimit] = kSecMatchLimitOne
            var item: CFTypeRef?
            if SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess,
               let data = item as? Data {
                return String(data: data, encoding: .utf8)
            }
            return nil
        }

        public func saveAPIKey(_ apiKey: String, for endpointID: UUID) async throws {
            let query = itemQuery(for: endpointID)
            let attributes: [CFString: Any] = [kSecValueData: Data(apiKey.utf8)]
            let updateStatus = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
            if updateStatus == errSecSuccess {
                return
            }
            guard updateStatus == errSecItemNotFound else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }

            var addQuery = query
            addQuery[kSecValueData] = Data(apiKey.utf8)
            let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
            if addStatus == errSecSuccess {
                return
            }
            guard addStatus == errSecDuplicateItem,
                  SecItemUpdate(query as CFDictionary, attributes as CFDictionary) == errSecSuccess
            else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }
        }

        public func clearAPIKey(for endpointID: UUID) async throws {
            let status = SecItemDelete(itemQuery(for: endpointID) as CFDictionary)
            guard status == errSecSuccess || status == errSecItemNotFound else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }
        }

        public func migrateLegacyAPIKey(to endpointID: UUID) async throws {
            let scopedQuery = itemQuery(for: endpointID)
            let scopedStatus = SecItemCopyMatching(scopedQuery as CFDictionary, nil)
            if scopedStatus == errSecSuccess {
                try await clearLegacyAPIKey()
                return
            }
            guard scopedStatus == errSecItemNotFound else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }

            var legacyQuery = legacyItemQuery()
            legacyQuery[kSecReturnData] = true
            legacyQuery[kSecMatchLimit] = kSecMatchLimitOne
            var item: CFTypeRef?
            let legacyStatus = SecItemCopyMatching(legacyQuery as CFDictionary, &item)
            if legacyStatus == errSecItemNotFound {
                return
            }
            guard legacyStatus == errSecSuccess, let data = item as? Data else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }

            var addQuery = scopedQuery
            addQuery[kSecValueData] = data
            let addStatus = SecItemAdd(addQuery as CFDictionary, nil)
            guard addStatus == errSecSuccess || addStatus == errSecDuplicateItem else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }
            try await clearLegacyAPIKey()
        }

        public func clearLegacyAPIKey() async throws {
            let status = SecItemDelete(legacyItemQuery() as CFDictionary)
            guard status == errSecSuccess || status == errSecItemNotFound else {
                throw LocalLLMSecretStoreError.keychainOperationFailed
            }
        }

        private func baseQuery() -> [CFString: Any] {
            [
                kSecClass: kSecClassGenericPassword,
                kSecAttrService: service
            ]
        }

        private func itemQuery(for endpointID: UUID) -> [CFString: Any] {
            var query = baseQuery()
            query[kSecAttrAccount] = account(for: endpointID)
            return query
        }

        private func legacyItemQuery() -> [CFString: Any] {
            var query = baseQuery()
            query[kSecAttrAccount] = "openai-compatible-api-key"
            return query
        }

        private func account(for endpointID: UUID) -> String {
            "openai-compatible-api-key-\(endpointID.uuidString.lowercased())"
        }
    }
#else
    public typealias KeychainLocalLLMSecretStore = InMemoryLocalLLMSecretStore
#endif
