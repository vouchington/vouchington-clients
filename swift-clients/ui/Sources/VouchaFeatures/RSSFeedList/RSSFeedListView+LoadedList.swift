import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension RSSFeedListView {
    var loadedListView: some View {
        List {
            if let admissionErrorMessageKey {
                Text(UiMessages.string(admissionErrorMessageKey, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .accessibilityAddTraits(.isStaticText)
            }
            ForEach(viewModel.feedRows) { row in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    rssFeedArticleCard(row.item)
                    storyRelatedArticlesView(for: row)
                    if let embed = viewModel.embedsByItemId[row.item.id] {
                        ProviderEmbedPreview(embed: embed)
                    }
                    if isSignedIn, let currentUserId {
                        HStack {
                            Spacer()
                            FollowerDistributionActions(
                                client: viewModel.client,
                                currentUserId: currentUserId,
                                target: .rssFeedItem(row.item.id)
                            )
                        }
                    }
                    if showsPlaybackAccessory, shouldShowPlaybackAccessory(for: row.item) {
                        RSSFeedPlaybackAccessoryView(
                            item: row.item,
                            isCurrentItem: playbackController.isCurrentItem(row.item),
                            isPlaying: playbackController.isCurrentItem(row.item) && playbackController.isPlaying,
                            onPlayPauseTap: {
                                _ = Task<Void, Never> { await playbackController.togglePlayback(for: row.item) }
                            }
                        )
                    }
                    if row.showsStory, isSignedIn, viewModel.canStartStoryDiscussion(rssFeedItemId: row.item.id) {
                        Button {
                            _ = Task<Void, Never> {
                                if let destination = await viewModel.startStoryDiscussion(rssFeedItemId: row.item.id) {
                                    storyDiscussionDestination = destination
                                }
                            }
                        } label: {
                            if viewModel.isStartingStoryDiscussion(rssFeedItemId: row.item.id) {
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
                        .disabled(viewModel.isStartingStoryDiscussion(rssFeedItemId: row.item.id))
                    }
                    if row.showsStory,
                       let destination = viewModel.storyDiscussionDestination(rssFeedItemId: row.item.id) {
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

    var admissionErrorMessageKey: UiMessageKey? {
        guard case let .error(.api(_, code)) = viewModel.state else { return nil }
        return NativeContributionAdmissionPresentation.messageKey(code)
    }

    var showsPlaybackAccessory: Bool {
        viewModel.contentType != .news
    }
}
