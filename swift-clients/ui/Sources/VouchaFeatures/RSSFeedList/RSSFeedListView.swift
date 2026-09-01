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

}

private extension RSSFeedListView {
    func shouldShowPlaybackAccessory(for item: RssFeedItem) -> Bool {
        guard case .embedOnlyVideo = item.playbackKind else {
            return true
        }
        return viewModel.embedsByItemId[item.id]?.approvedPlayerWithSource == nil
    }

    var admissionErrorMessageKey: UiMessageKey? {
        guard case let .error(.api(_, code)) = viewModel.state else { return nil }
        return NativeContributionAdmissionPresentation.messageKey(code)
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
