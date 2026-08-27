#if DEBUG
    import Foundation
    import VouchaAPI
    import VouchaAuth
    import VouchaCore
    import VouchaPersistence

    extension ViewModelFactory {
        static func makeIsolatedUITesting(config: AppConfig) -> ViewModelFactory {
            let cookieStorage = HTTPCookieStorage()
            let client = APIClient(
                config: config,
                cookieStorage: cookieStorage,
                bootstrapSession: false,
                metadata: NativeClientMetadata.current()
            )
            return ViewModelFactory(
                apiClient: client,
                sessionManager: SessionManager(client: client, cookieStorage: cookieStorage),
                cacheStore: MemoryDiskCacheStore(),
                featureFlagOverrideStore: UITestingFeatureFlagOverrideStore(),
                config: config,
                nativeOAuthAuthorizationStore: NativeOAuthAuthorizationStore(
                    secureState: UITestingOAuthAuthorizationSecureState()
                ),
                restoresSessionOnLaunch: false
            )
        }
    }
#endif
