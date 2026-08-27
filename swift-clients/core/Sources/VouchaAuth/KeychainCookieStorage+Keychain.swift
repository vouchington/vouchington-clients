import Foundation

#if canImport(Security)
    extension KeychainCookieStorage {
        public convenience init(serviceName: String = "ai.voucha", accessGroup: String? = nil) {
            self.init(serviceName: serviceName, accessGroup: accessGroup, backend: .security)
        }

        convenience init(
            serviceName: String,
            accessGroup: String?,
            backend: KeychainCookiePersistence.Backend
        ) {
            let keychainKey = "\(serviceName).cookies"
            self.init(
                loadPersistedCookies: {
                    KeychainCookiePersistence.load(
                        serviceName: serviceName,
                        keychainKey: keychainKey,
                        accessGroup: accessGroup,
                        backend: backend
                    )
                },
                savePersistedCookies: {
                    KeychainCookiePersistence.save(
                        $0,
                        serviceName: serviceName,
                        keychainKey: keychainKey,
                        accessGroup: accessGroup,
                        backend: backend
                    )
                },
                deletePersistedCookies: {
                    KeychainCookiePersistence.delete(
                        serviceName: serviceName,
                        keychainKey: keychainKey,
                        accessGroup: accessGroup,
                        backend: backend
                    )
                }
            )
        }
    }
#endif
