import SwiftUI
import VouchaAuth
import VouchaDesignSystem
import VouchaFeatures
import VouchaLocalization

/// Root layout.
///
/// macOS/iPad: Three-column `NavigationSplitView`.
///   - Column 1 (sidebar): AppSection list.
///   - Column 2 (content): Sub-options for the selected vertical; hidden for non-verticals.
///   - Column 3 (detail): Content view for the selected sub-option.
///
/// iOS (compact): `TabView` bottom bar with a per-vertical segmented picker in the
/// navigation bar. Each tab lands directly on the primary sub-option (Your Feed).
@MainActor
public struct RootView: View {
    #if DEBUG
        public struct ExternalRouteDispatch: Equatable {
            let targetPath: String
            let generation: Int
        }
    #endif

    public static let rootShellAccessibilityIdentifier = "root-shell"
    @State
    var selectedSection: AppSection? = .news
    /// macOS only: driven by column-2 sub-option list selection.
    @State
    var selectedSubsection: VerticalSubsection?
    @State
    var selectedNativeRouteEntry: NativeRouteCatalogEntry?
    @State
    var selectedNativeRouteMatch: NativeRouteMatch?
    @State
    var selectedNativeRouteQuery: String?
    @State
    var nativeRouteDispatchGeneration = 0
    @State
    var pendingFeatureFlagRouteURL: URL?
    @State
    var pendingNativeRouteURL: URL?
    @State
    var signInLinkEmail: String?
    @State
    var signInLinkCode: String?
    @State
    var featureFlagState: FeatureFlagState
    @State
    var sectionPreferences = AppSectionPreferenceStore.load()
    @State
    var showingCustomizeNavigation = false
    @State
    var showingSignIn = false
    @State
    var podcastPlaybackController: PodcastPlaybackController
    @Environment(\.openURL)
    var openURL
    @Environment(\.scenePhase)
    var scenePhase
    #if !os(macOS)
        @Environment(\.horizontalSizeClass)
        var horizontalSizeClass
    #endif
    let viewModelFactory: ViewModelFactory
    #if DEBUG
        let externalRouteDispatch: ExternalRouteDispatch?
    #endif

    #if DEBUG
        public init(
            viewModelFactory: ViewModelFactory,
            externalRouteDispatch: ExternalRouteDispatch? = nil
        ) {
            self.viewModelFactory = viewModelFactory
            self.externalRouteDispatch = externalRouteDispatch
            _featureFlagState = State(initialValue: viewModelFactory.featureFlagState)
            _podcastPlaybackController = State(initialValue: viewModelFactory.makePodcastPlaybackController())
        }
    #else
        public init(viewModelFactory: ViewModelFactory) {
            self.viewModelFactory = viewModelFactory
            _featureFlagState = State(initialValue: viewModelFactory.featureFlagState)
            _podcastPlaybackController = State(initialValue: viewModelFactory.makePodcastPlaybackController())
        }
    #endif

    public var body: some View {
        appBody
            .environment(viewModelFactory.uiLocaleController)
            .environment(\.locale, viewModelFactory.uiLocaleController.locale.foundationLocale)
            .safeAreaInset(edge: .bottom, spacing: 0) {
                if podcastPlaybackController.currentItem != nil {
                    PodcastMiniPlayerView(controller: podcastPlaybackController)
                }
            }
            .task {
                await restoreSessionAndNativeAuthorizationsOnLaunch()
                await refreshLocalization()
                await refreshFeatureFlags()
                let nativeOAuthCoordinator = viewModelFactory.nativeOAuthAuthorizationCoordinator
                handleNativeOAuthAuthorizationResult(nativeOAuthCoordinator.result)
            }
            .onChange(of: viewModelFactory.sessionManager.uiLocale) { _, uiLocale in
                viewModelFactory.uiLocaleController.update(savedUiLocale: uiLocale)
                Task { await refreshLocalization() }
            }
            .onChange(of: scenePhase) { _, phase in
                guard phase == .active else { return }
                Task { await refreshLocalization() }
            }
            .onChange(of: viewModelFactory.nativeOAuthAuthorizationCoordinator.result) { _, result in
                handleNativeOAuthAuthorizationResult(result)
            }
        #if DEBUG
            .onChange(of: externalRouteDispatch) { _, dispatch in
                if let dispatch {
                    routeNativeTargetPath(dispatch.targetPath)
                }
            }
        #endif
    }
}
