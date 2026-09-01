import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeFocusedRssFeedItemSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel
    let playbackController: PodcastPlaybackController?
    let currentUserId: String?

    init(
        viewModel: NativeRouteSurfaceViewModel,
        playbackController: PodcastPlaybackController?,
        currentUserId: String? = nil
    ) {
        self.viewModel = viewModel
        self.playbackController = playbackController
        self.currentUserId = currentUserId
    }

    var body: some View {
        switch viewModel.state {
        case .loading, .idle:
            ProgressView()
                .frame(maxWidth: .infinity)
                .padding(.vertical, Spacing.xl)
        case let .error(error):
            ErrorStateView(error: error) {
                await viewModel.load()
            }
        case .loaded:
            if let item = viewModel.focusedRssFeedItem {
                focusedContent(item)
            } else {
                EmptyStateView(
                    icon: "exclamationmark.triangle",
                    title: .message(.nativeSwiftRebasedRouteSurfacesRssItemUnavailable),
                    message: .message(.nativeSwiftRebasedRouteSurfacesRssItemUnavailableMessage)
                )
            }
        }
    }

    private func focusedContent(_ item: RssFeedItem) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            RssFeedItemCard(
                item: item,
                variant: .expanded,
                election: viewModel.focusedRssFeedItemElection,
                myVote: viewModel.focusedRssFeedItemVote,
                isSaved: viewModel.focusedRssFeedItemBookmarks["save"] == true,
                isHidden: viewModel.focusedRssFeedItemBookmarks["hide"] == true,
                showsDescription: false,
                apiBaseURL: viewModel.client?.baseURL ?? AppConfig.shared.baseURL
            )
            if let embed = viewModel.focusedRssFeedItemEmbed {
                ProviderEmbedPreview(embed: embed)
            }
            distributionActions(for: item)

            NativeHtmlContent(
                html: viewModel.focusedRssFeedItemContentHtml,
                fallback: item.description
            )

            if viewModel.focusedRssFeedItemBookmarks["save"] == true {
                Label(
                    UiMessages.string(.nativeSwiftDesignSystemSaved, locale: nativeUiLocale),
                    systemImage: "bookmark.fill"
                )
                .accessibilityIdentifier("focused-rss-item-saved")
            }
            if viewModel.focusedRssFeedItemBookmarks["hide"] == true {
                Label(
                    UiMessages.string(.nativeSwiftRebasedRouteSurfacesHidden, locale: nativeUiLocale),
                    systemImage: "eye.slash.fill"
                )
                .accessibilityIdentifier("focused-rss-item-hidden")
            }

            playbackAccessory(for: item)

            if viewModel.focusedRssFeedItemEmbed?.validatedSourceURL == nil,
               let externalURLString = item.externalURLString,
               let externalURL = URL(string: externalURLString) {
                Link(destination: externalURL) {
                    Label(
                        UiMessages.string(.nativeSwiftPodcastPlaybackOpenSource, locale: nativeUiLocale),
                        systemImage: "arrow.up.right.square"
                    )
                }
            }

            HnDiscussionsPanel(
                urls: [item.externalURLString].compactMap { $0 },
                client: viewModel.client
            )
        }
    }

    @ViewBuilder
    private func distributionActions(for item: RssFeedItem) -> some View {
        if let client = viewModel.client, let currentUserId {
            HStack {
                Spacer()
                FollowerDistributionActions(
                    client: client,
                    currentUserId: currentUserId,
                    target: .rssFeedItem(item.id)
                )
            }
        }
    }

    @ViewBuilder
    private func playbackAccessory(for item: RssFeedItem) -> some View {
        switch item.playbackKind {
        case .embedOnlyVideo:
            if viewModel.focusedRssFeedItemEmbed?.approvedPlayerWithSource == nil {
                Text(UiMessages.string(.nativeSwiftRssFeedPlaybackVideoUnavailable, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        case .audio, .directVideo:
            if let playbackController {
                RSSFeedPlaybackAccessoryView(
                    item: item,
                    isCurrentItem: playbackController.isCurrentItem(item),
                    isPlaying: playbackController.isCurrentItem(item) && playbackController.isPlaying,
                    onPlayPauseTap: {
                        Task { await playbackController.togglePlayback(for: item) }
                    },
                    showsSourceLink: false
                )
            }
        case .externalAudio, .unavailable:
            EmptyView()
        }
    }
}
