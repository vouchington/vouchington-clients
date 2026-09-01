import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension RSSFeedListView {
    var loadedListView: some View {
        List {
            if let admissionErrorMessageKey {
                Text(UiMessages.string(admissionErrorMessageKey, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .accessibilityAddTraits(.isStaticText)
            }
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
