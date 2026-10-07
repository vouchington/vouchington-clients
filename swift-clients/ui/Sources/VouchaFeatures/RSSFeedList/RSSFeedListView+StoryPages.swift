import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension RSSFeedListView {
    @ViewBuilder
    func storyRelatedArticlesView(for item: RssFeedItem) -> some View {
        if let related = viewModel.relatedArticles(rssFeedItemId: item.id),
           !related.pagination.items.isEmpty || related.pagination.hasMore {
            Button {
                related.isExpanded.toggle()
            } label: {
                Label(
                    UiMessages.string(UiMessage(
                        related.pagination.hasMore ? .nativeCommonRelatedArticlesMore : .nativeCommonRelatedArticles,
                        numberParameters: ["count": Double(related.pagination.items.count)]
                    ), locale: nativeUiLocale),
                    systemImage: related.isExpanded ? "chevron.up" : "chevron.down"
                )
            }
            .buttonStyle(.bordered)
            .accessibilityIdentifier("story-related-\(item.id)")
            if related.isExpanded {
                ForEach(related.pagination.items.filter { !viewModel.hiddenItemIds.contains($0.id) }) { peer in
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
                                related.pagination.lastError == nil ? .nativeSwiftCommonLoadMore : .nativeCommonRetry,
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
