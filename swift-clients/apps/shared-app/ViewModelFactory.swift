import Foundation
#if canImport(Security)
    import Security
#endif
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaFeatures
import VouchaLocalization
import VouchaPersistence

/// Dependency-injection factory for all feature view models.
/// Holds the shared APIClient, SessionManager, and CacheStore.
@MainActor
public final class ViewModelFactory {
    private static let keychainAccessGroupSuffix = "ai.voucha.macos"

    /// The shared API client used for all HTTP requests.
    public let apiClient: APIClient
    /// The shared session manager tracking sign-in state.
    public let sessionManager: SessionManager
    /// The shared observable UI locale used by every native presentation surface.
    public let uiLocaleController: UiLocaleController
    /// The shared cache store for persisted data.
    public let cacheStore: any CacheStore
    /// The app configuration (base URL, image CDN URL).
    public let config: AppConfig
    public let featureFlagState: FeatureFlagState
    public let nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator
    public let restoresSessionOnLaunch: Bool
    /// Shared App Attest service — nil on platforms without DeviceCheck.
    private var appAttestationService: AppAttestationService?

    /// Creates a factory with explicitly supplied dependencies (for testing).
    public init(
        apiClient: APIClient,
        sessionManager: SessionManager,
        cacheStore: any CacheStore,
        featureFlagOverrideStore: any FeatureFlagOverridePersisting = FeatureFlagOverrideFileStore.applicationSupport(),
        config: AppConfig = .shared,
        nativeOAuthAuthorizationStore: NativeOAuthAuthorizationStore,
        restoresSessionOnLaunch: Bool = true
    ) {
        self.apiClient = apiClient
        self.sessionManager = sessionManager
        uiLocaleController = UiLocaleController(savedUiLocale: sessionManager.uiLocale)
        self.cacheStore = cacheStore
        nativeOAuthAuthorizationCoordinator = NativeOAuthAuthorizationCoordinator(
            client: apiClient,
            sessionManager: sessionManager,
            store: nativeOAuthAuthorizationStore
        )
        featureFlagState = FeatureFlagState(store: featureFlagOverrideStore)
        self.config = config
        self.restoresSessionOnLaunch = restoresSessionOnLaunch
    }

    /// Creates a factory wired to the production-ready default dependencies.
    public static func makeDefault(config: AppConfig = .shared) -> ViewModelFactory {
        #if DEBUG
            if ProcessInfo.processInfo.arguments.contains(where: { $0.hasPrefix("--ui-testing-") }) {
                return makeIsolatedUITesting(config: config)
            }
        #endif
        let keychainAccessGroup = keychainAccessGroup()
        let cookieStorage = KeychainCookieStorage(accessGroup: keychainAccessGroup)
        #if canImport(DeviceCheck)
            let sharedKeyStore = AppAttestKeyStore(accessGroup: keychainAccessGroup)
            let requestSigner = AppAttestRequestSigner(
                provider: DeviceCheckAppAttestProvider(),
                keyStore: sharedKeyStore,
                isEnabled: { !UserDefaults.standard.bool(forKey: "requestSigningDisabled") }
            )
            let client = APIClient(
                config: config,
                cookieStorage: cookieStorage,
                signer: requestSigner,
                metadata: NativeClientMetadata.current()
            )
            let appAttestationService = AppAttestationService(
                client: client,
                provider: DeviceCheckAppAttestProvider(),
                keyStore: sharedKeyStore
            )
        #else
            let client = APIClient(
                config: config,
                cookieStorage: cookieStorage,
                metadata: NativeClientMetadata.current()
            )
        #endif
        let session = SessionManager(client: client, cookieStorage: cookieStorage)
        let cache = MemoryDiskCacheStore()
        let factory = ViewModelFactory(
            apiClient: client,
            sessionManager: session,
            cacheStore: cache,
            config: config,
            nativeOAuthAuthorizationStore: NativeOAuthAuthorizationStore()
        )
        #if canImport(DeviceCheck)
            factory.appAttestationService = appAttestationService
        #endif
        return factory
    }

    private static func keychainAccessGroup() -> String? {
        #if os(macOS) || targetEnvironment(macCatalyst)
            guard let task = SecTaskCreateFromSelf(nil) else { return nil }
            guard let groups = SecTaskCopyValueForEntitlement(
                task,
                "keychain-access-groups" as CFString,
                nil
            ) as? [String] else {
                #if DEBUG
                    missingKeychainAccessGroupFailure()
                #endif
                return nil
            }
            guard let group = groups.first(where: { $0.hasSuffix(keychainAccessGroupSuffix) }) else {
                #if DEBUG
                    missingKeychainAccessGroupFailure()
                #endif
                return nil
            }
            return group
        #else
            return nil
        #endif
    }

    #if DEBUG && (os(macOS) || targetEnvironment(macCatalyst))
        private static func missingKeychainAccessGroupFailure() {
            assertionFailure("Missing keychain access group ending in \(keychainAccessGroupSuffix).")
        }
    #endif

    /// Creates a view model for the RSS feed list with the given content type and feed scope.
    ///
    /// - Parameters:
    ///   - contentType: Filters by media type (article/audio/video).
    ///   - feedSource: `.your` loads the personalized feed; `.all` loads the global feed.
    public func makeRSSFeedListViewModel(
        contentType: ContentType,
        feedSource: FeedSource = .your
    ) -> RSSFeedListViewModel {
        RSSFeedListViewModel(client: apiClient, contentType: contentType, feedSource: feedSource)
    }

    /// Creates a view model for the posts feed.
    ///
    /// - Parameter feedType: Feed path segment (e.g. `"any"`, `"follow_users"`). Defaults to `"any"`.
    public func makePostsListViewModel(feedType: String = "any") -> PostsListViewModel {
        PostsListViewModel(client: apiClient, feedType: feedType)
    }

    /// Creates a view model for the notifications feed.
    public func makeNotificationsListViewModel() -> NotificationsListViewModel {
        NotificationsListViewModel(client: apiClient)
    }

    /// Creates a view model for the Friends tab.
    public func makeFriendsListViewModel() -> FriendsListViewModel {
        FriendsListViewModel(client: apiClient, userId: sessionManager.currentUserId, config: config)
    }

    /// Creates a view model for the Profile tab.
    public func makeProfileViewModel() -> ProfileViewModel {
        ProfileViewModel(client: apiClient, config: config, sessionManager: sessionManager)
    }

    /// Creates a sign-in service backed by the shared client and session manager.
    public func makeSignInService() -> SignInService {
        SignInService(client: apiClient, sessionManager: sessionManager, appAttestationService: appAttestationService)
    }

    /// Creates the shared podcast playback controller mounted at the root shell.
    public func makePodcastPlaybackController() -> PodcastPlaybackController {
        PodcastPlaybackController(client: apiClient, sessionManager: sessionManager)
    }
}
