import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct RSSFeedListView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: RSSFeedListViewModel
    @Bindable
    public var playbackController: PodcastPlaybackController
    public let isSignedIn: Bool
    public let canVote: Bool
    public let currentUserId: String?
    public let showSignIn: (() -> Void)?
    @State
    var storyDiscussionDestination: StoryDiscussionDestination?

    public init(
        viewModel: RSSFeedListViewModel,
        playbackController: PodcastPlaybackController,
        isSignedIn: Bool = true,
        canVote: Bool? = nil,
        currentUserId: String? = nil,
        showSignIn: (() -> Void)? = nil
    ) {
        self.viewModel = viewModel
        self.playbackController = playbackController
        self.isSignedIn = isSignedIn
        self.canVote = canVote ?? isSignedIn
        self.currentUserId = currentUserId
        self.showSignIn = showSignIn
    }

    public var body: some View {
        Group {
            if viewModel.items.isEmpty {
                emptyOrLoadingView
            } else {
                loadedListView
            }
        }
        .navigationTitle(UiMessages.string(navigationTitle, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .refreshable { await viewModel.reload() }
        .navigationDestination(item: $storyDiscussionDestination) { destination in
            storyDiscussionDestinationView(destination)
        }
        .emailVerificationRecovery(
            client: viewModel.client,
            gate: viewModel.emailVerificationGate
        )
    }

    @ViewBuilder
    private var emptyOrLoadingView: some View {
        switch viewModel.state {
        case .loading:
            LoadingView()
        case let .error(error):
            ErrorStateView(error: error) {
                await viewModel.reload()
            }
        default:
            EmptyStateView(
                icon: "tray",
                title: viewModel.contentType.emptyTitle,
                message: viewModel.contentType.emptyMessage
            )
        }
    }

    private var loadedListView: some View {
        List {
            ForEach(viewModel.items) { item in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    RssFeedItemCard(
                        item: item,
                        variant: .compact,
                        election: viewModel.election(for: item.id),
                        myVote: viewModel.myVotesByItemId[item.id],
                        canCreateVote: canVote,
                        onVote: isSignedIn
                            ? { choice in
                                _ = Task<Void, Never> { await viewModel.vote(rssFeedItemId: item.id, choice: choice) }
                            }
                            : nil,
                        onSignedOutTap: isSignedIn ? nil : showSignIn,
                        isSaved: viewModel.isSaved(rssFeedItemId: item.id),
                        isHidden: viewModel.isHidden(rssFeedItemId: item.id),
                        onToggleSaved: isSignedIn ?
                            { _ = Task<Void, Never> { await viewModel.toggleSave(rssFeedItemId: item.id) } } : nil,
                        onToggleHidden: isSignedIn ?
                            { _ = Task<Void, Never> { await viewModel.toggleHide(rssFeedItemId: item.id) } } : nil,
                        apiBaseURL: viewModel.apiBaseURL
                    )
                    if let embed = viewModel.embedsByItemId[item.id] {
                        ProviderEmbedPreview(embed: embed)
                    }
                    if isSignedIn, let currentUserId {
                        HStack {
                            Spacer()
                            FollowerDistributionActions(
                                client: viewModel.client,
                                currentUserId: currentUserId,
                                target: .rssFeedItem(item.id)
                            )
                        }
                    }
                    if showsPlaybackAccessory, shouldShowPlaybackAccessory(for: item) {
                        RSSFeedPlaybackAccessoryView(
                            item: item,
                            isCurrentItem: playbackController.isCurrentItem(item),
                            isPlaying: playbackController.isCurrentItem(item) && playbackController.isPlaying,
                            onPlayPauseTap: {
                                _ = Task<Void, Never> { await playbackController.togglePlayback(for: item) }
                            }
                        )
                    }
                    if isSignedIn, viewModel.canStartStoryDiscussion(rssFeedItemId: item.id) {
                        Button {
                            _ = Task<Void, Never> {
                                if let destination = await viewModel.startStoryDiscussion(rssFeedItemId: item.id) {
                                    storyDiscussionDestination = destination
                                }
                            }
                        } label: {
                            if viewModel.isStartingStoryDiscussion(rssFeedItemId: item.id) {
                                ProgressView()
                            } else {
                                Label(
                                    UiMessages
                                        .string(.nativeSwiftRssFeedListDiscussTheFullStory, locale: nativeUiLocale),
                                    systemImage: "text.bubble"
                                )
                            }
                        }
                        .buttonStyle(.bordered)
                        .disabled(viewModel.isStartingStoryDiscussion(rssFeedItemId: item.id))
                    }
                    if let destination = viewModel.storyDiscussionDestination(rssFeedItemId: item.id) {
                        openStoryDiscussionButton(destination)
                    }
                }
            }
            HybridPaginationControl(
                hasMore: viewModel.hasMore,
                isLoading: viewModel.isLoading,
                hasError: viewModel.hasPaginationError
            ) {
                await viewModel.loadNextPage()
            }
            .listRowSeparator(.hidden)
        }
        .listStyle(.plain)
    }
}

private extension RSSFeedListView {
    func shouldShowPlaybackAccessory(for item: RssFeedItem) -> Bool {
        guard case .embedOnlyVideo = item.playbackKind else {
            return true
        }
        return viewModel.embedsByItemId[item.id]?.approvedPlayerWithSource == nil
    }

    var showsPlaybackAccessory: Bool {
        viewModel.contentType != .news
    }

    var navigationTitle: UiMessageKey {
        switch viewModel.contentType {
        case .news: .nativeSwiftNavigationTitlesNews
        case .video: .nativeSwiftNavigationTitlesVideos
        case .podcast: .nativeSwiftNavigationTitlesPodcasts
        }
    }
}
