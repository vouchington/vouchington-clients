import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension RSSFeedListView {
    @ViewBuilder
    func storyRelatedArticlesView(for row: RssFeedListRow) -> some View {
        let item = row.item
        if row.showsStory, let related = viewModel.relatedArticles(rssFeedItemId: item.id),
           related.pagination.hasMore || related.pagination.items.contains(where: {
               !viewModel.hiddenItemIds.contains($0.id)
           }) {
            let visiblePeers = related.pagination.items.filter { !viewModel.hiddenItemIds.contains($0.id) }
            Button {
                related.isExpanded.toggle()
            } label: {
                Label(
                    UiMessages.string(UiMessage(
                        related.pagination
                            .hasMore ? .nativeCommonRelatedArticlesMore : .nativeCommonRelatedArticles,
                        numberParameters: ["count": Double(visiblePeers.count)]
                    ), locale: nativeUiLocale),
                    systemImage: related.isExpanded ? "chevron.up" : "chevron.down"
                )
            }
            .buttonStyle(.bordered)
            .accessibilityIdentifier("story-related-\(item.id)")
            if related.isExpanded {
                ForEach(visiblePeers) { peer in
                    rssFeedArticleCard(peer)
                    if let embed = viewModel.embedsByItemId[peer.id] {
                        ProviderEmbedPreview(embed: embed)
                    }
                }
                if related.pagination.hasMore {
                    Button {
                        _ = Task<Void, Never> { await viewModel.loadMoreStoryArticles(rssFeedItemId: item.id) }
                    } label: {
                        if related.pagination.isLoading {
                            ProgressView()
                        } else {
                            Text(UiMessages.string(
                                related.pagination
                                    .lastError == nil ? .nativeSwiftCommonLoadMore : .nativeCommonRetry,
                                locale: nativeUiLocale
                            ))
                        }
                    }
                    .disabled(related.pagination.isLoading)
                    .accessibilityIdentifier("story-load-more-\(item.id)")
                }
            }
        }
    }

    func rssFeedArticleCard(_ item: RssFeedItem) -> some View {
        RssFeedItemCard(
            item: item, variant: .compact,
            election: viewModel.election(for: item.id), myVote: viewModel.myVotesByItemId[item.id],
            canCreateVote: canVote,
            onVote: isSignedIn ? { choice in
                _ = Task<Void, Never> { await viewModel.vote(rssFeedItemId: item.id, choice: choice) }
            } : nil,
            onSignedOutTap: isSignedIn ? nil : showSignIn,
            isSaved: viewModel.isSaved(rssFeedItemId: item.id), isHidden: viewModel.isHidden(rssFeedItemId: item.id),
            onToggleSaved: isSignedIn ? {
                _ = Task<Void, Never> { await viewModel.toggleSave(rssFeedItemId: item.id) }
            } : nil,
            onToggleHidden: isSignedIn ? {
                _ = Task<Void, Never> { await viewModel.toggleHide(rssFeedItemId: item.id) }
            } : nil,
            apiBaseURL: viewModel.apiBaseURL
        )
    }
}
