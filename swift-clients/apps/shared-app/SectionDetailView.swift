import SwiftUI
import VouchaFeatures

// MARK: - SectionDetailView

/// Routes each `AppSection` to its content view.
///
/// All view models are stored as `@State` properties so pagination and scroll position
/// are preserved when the user navigates away and back. On macOS, sub-option selection
/// is driven externally (column-2 list in `RootView`). On iOS, a toolbar segmented
/// picker inside this view drives sub-option selection.
@MainActor
struct SectionDetailView: View {
    @Environment(\.locale)
    var nativeUiLocale
    let section: AppSection
    /// macOS only: the sub-option selected in column 2. Nil on iOS.
    let macOSSubsection: VerticalSubsection?
    let nativeRouteEntry: NativeRouteCatalogEntry?
    let nativeRouteMatch: NativeRouteMatch?
    let nativeRouteQuery: String?
    let nativeRouteDispatchGeneration: Int
    let factory: ViewModelFactory
    let playbackController: PodcastPlaybackController
    let onNavigateToTargetPath: (String) -> Void
    let customizeNavigation: () -> Void
    let showSignIn: () -> Void

    /// iOS internal subsection state (toolbar segmented picker).
    @State
    var iOSSubsection: VerticalSubsection?
    @State
    var didClearNativeRoute = false
    @State
    var nativeRouteHistory: [String] = []

    /// RSS feed VMs — one per content-type × scope.
    @State
    var newsFeedYourVM: RSSFeedListViewModel
    @State
    var newsFeedAllVM: RSSFeedListViewModel
    @State
    var podcastFeedYourVM: RSSFeedListViewModel
    @State
    var podcastFeedAllVM: RSSFeedListViewModel
    @State
    var videoFeedYourVM: RSSFeedListViewModel
    @State
    var videoFeedAllVM: RSSFeedListViewModel

    /// Sources VMs — one per content-type (caches both your/all scopes internally).
    @State
    var newsSourcesVM: SourcesListViewModel
    @State
    var podcastSourcesVM: SourcesListViewModel
    @State
    var videoSourcesVM: SourcesListViewModel

    /// Posts VMs — one per feed scope.
    @State
    var postsYourVM: PostsListViewModel
    @State
    var postsAllVM: PostsListViewModel

    /// Non-vertical VMs.
    @State
    var notificationsVM: NotificationsListViewModel
    @State
    var friendsVM: FriendsListViewModel
    @State
    var profileVM: ProfileViewModel

    init(
        section: AppSection,
        macOSSubsection: VerticalSubsection?,
        nativeRouteEntry: NativeRouteCatalogEntry? = nil,
        nativeRouteMatch: NativeRouteMatch? = nil,
        nativeRouteQuery: String? = nil,
        nativeRouteDispatchGeneration: Int = 0,
        factory: ViewModelFactory,
        playbackController: PodcastPlaybackController,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        customizeNavigation: @escaping () -> Void = {},
        showSignIn: @escaping () -> Void = {}
    ) {
        self.section = section
        self.macOSSubsection = macOSSubsection
        self.nativeRouteEntry = nativeRouteEntry
        self.nativeRouteMatch = nativeRouteMatch
        self.nativeRouteQuery = nativeRouteQuery
        self.nativeRouteDispatchGeneration = nativeRouteDispatchGeneration
        self.factory = factory
        self.playbackController = playbackController
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.customizeNavigation = customizeNavigation
        self.showSignIn = showSignIn

        let userId = factory.sessionManager.currentUserId
        let client = factory.apiClient

        _newsFeedYourVM = State(initialValue: factory.makeRSSFeedListViewModel(contentType: .news, feedSource: .your))
        _newsFeedAllVM = State(initialValue: factory.makeRSSFeedListViewModel(contentType: .news, feedSource: .all))
        _podcastFeedYourVM = State(
            initialValue: factory.makeRSSFeedListViewModel(contentType: .podcast, feedSource: .your)
        )
        _podcastFeedAllVM = State(
            initialValue: factory.makeRSSFeedListViewModel(contentType: .podcast, feedSource: .all)
        )
        _videoFeedYourVM = State(initialValue: factory.makeRSSFeedListViewModel(contentType: .video, feedSource: .your))
        _videoFeedAllVM = State(initialValue: factory.makeRSSFeedListViewModel(contentType: .video, feedSource: .all))

        _newsSourcesVM = State(
            initialValue: SourcesListViewModel(client: client, userId: userId, feedType: AppSection.news.sourceFeedType)
        )
        _podcastSourcesVM = State(
            initialValue: SourcesListViewModel(
                client: client, userId: userId, feedType: AppSection.podcasts.sourceFeedType
            )
        )
        _videoSourcesVM = State(
            initialValue: SourcesListViewModel(
                client: client, userId: userId, feedType: AppSection.videos.sourceFeedType
            )
        )

        _postsYourVM = State(initialValue: factory.makePostsListViewModel(feedType: "any"))
        _postsAllVM = State(initialValue: factory.makePostsListViewModel(feedType: "all"))

        _notificationsVM = State(initialValue: factory.makeNotificationsListViewModel())
        _friendsVM = State(initialValue: factory.makeFriendsListViewModel())
        _profileVM = State(initialValue: factory.makeProfileViewModel())
    }
}
