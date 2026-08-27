import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

/// A source list for one scope (Your Sources or All Sources).
///
/// The `scope` parameter drives which scope is active on the view model via
/// `.task(id: scope)`. The caller (macOS: NavigationSplitView column 2; iOS: toolbar
/// picker in VerticalContainerView) is responsible for switching the scope.
public struct SourcesListView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: SourcesListViewModel
    /// The scope to display. When this changes, `.task(id: scope)` re-fires and loads.
    public let scope: SourceScope
    public let navigationTitle: String
    public let isSignedIn: Bool
    public let canVote: Bool
    public let showSignIn: (() -> Void)?

    public init(
        viewModel: SourcesListViewModel,
        scope: SourceScope,
        navigationTitle: String,
        isSignedIn: Bool = true,
        canVote: Bool? = nil,
        showSignIn: (() -> Void)? = nil
    ) {
        self.viewModel = viewModel
        self.scope = scope
        self.navigationTitle = navigationTitle
        self.isSignedIn = isSignedIn
        self.canVote = canVote ?? isSignedIn
        self.showSignIn = showSignIn
    }

    public var body: some View {
        Group {
            if currentItems.isEmpty {
                VStack(alignment: .leading, spacing: Spacing.md) {
                    statusBanner
                    emptyOrLoadingView
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            } else {
                loadedListView
                    .safeAreaInset(edge: .top, spacing: Spacing.sm) {
                        statusBanner
                    }
            }
        }
        .navigationTitle(navigationTitle)
        .task(id: scope) {
            viewModel.scope = scope
            await viewModel.load()
        }
        .refreshable { await viewModel.reload() }
    }

    @ViewBuilder
    private var statusBanner: some View {
        if let message = viewModel.statusMessage {
            Text(verbatim: UiMessages.string(message, locale: nativeUiLocale))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
        }
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
                icon: "antenna.radiowaves.left.and.right",
                title: .message(.nativeSwiftEmptyStateNoSources),
                message: emptyMessage
            )
        }
    }

    private var loadedListView: some View {
        List {
            ForEach(currentItems) { source in
                RssFeedSourceCard(
                    source: source,
                    isFollowing: viewModel.isFollowing(sourceId: source.id),
                    isFollowingTopic: viewModel.isFollowingTopic(topicId: source.topic?.id),
                    isMutedSource: viewModel.isMuted(sourceId: source.id),
                    isMutedTopic: viewModel.isMutedTopic(topicId: source.topic?.id),
                    followAction: viewModel.canToggleFollow
                        ? { Task<Void, Never> { await viewModel.toggleFollow(sourceId: source.id) } }
                        : nil,
                    muteAction: viewModel.canToggleFollow
                        ? { Task<Void, Never> { await viewModel.toggleMute(sourceId: source.id) } }
                        : nil,
                    followTopicAction: viewModel.canToggleFollow && source.topic != nil
                        ? { Task<Void, Never> { await viewModel.toggleFollowTopic(topicId: source.topic?.id) } }
                        : nil,
                    muteTopicAction: viewModel.canToggleFollow && source.topic != nil
                        ? { Task<Void, Never> { await viewModel.toggleMuteTopic(topicId: source.topic?.id) } }
                        : nil,
                    topicElection: viewModel.topicElection(for: source.topic?.id),
                    myTopicVote: source.topic.flatMap { viewModel.myVotesByTopicId[$0.id] },
                    canCreateTopicVote: canVote,
                    onTopicVote: isSignedIn && viewModel.canToggleFollow && source.topic != nil
                        ? { choice in
                            guard let topicId = source.topic?.id else { return }
                            _ = Task<Void, Never> { await viewModel.vote(topicId: topicId, choice: choice) }
                        }
                        : nil,
                    onTopicSignedOutTap: isSignedIn ? nil : showSignIn
                )
            }
        }
        .listStyle(.plain)
    }

    private var currentItems: [RssFeedSource] {
        switch scope {
        case .your: viewModel.yourSources
        case .all: viewModel.allSources
        }
    }

    private var emptyMessage: UiVerbatimText {
        switch scope {
        case .your: .message(.nativeSwiftEmptyStateFollowSourcesMessage)
        case .all: .message(.nativeSwiftEmptyStateNoSourcesAvailableMessage)
        }
    }
}
