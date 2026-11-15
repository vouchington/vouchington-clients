import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatBookmarkFactView: View {
    let bookmark: NativeChatBookmarkFact
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(
                bookmark.action == .set ? .nativeMcpFactsBookmarkSet : .nativeMcpFactsBookmarkRemoved,
                locale: locale
            ))
            Text(UiMessages.string(
                .joined([.message(predicateKey)] + bookmark.entityTypes.map { .message(entityTypeKey($0)) }),
                locale: locale
            ))
            .foregroundStyle(Colors.secondaryLabel)
        }
        .accessibilityIdentifier("native-chat-bookmark-fact")
    }

    private var predicateKey: UiMessageKey {
        switch bookmark.predicate {
        case .save: .nativeMcpFactsBookmarkPredicateSave
        case .follow: .nativeMcpFactsBookmarkPredicateFollow
        case .mute: .nativeMcpFactsBookmarkPredicateMute
        case .block: .nativeMcpFactsBookmarkPredicateBlock
        }
    }

    private func entityTypeKey(_ type: NativeChatBookmarkEntityType) -> UiMessageKey {
        switch type {
        case .post: .nativeMcpFactsBookmarkEntityTypePost
        case .topic: .nativeMcpFactsBookmarkEntityTypeTopic
        case .user: .nativeMcpFactsBookmarkEntityTypeUser
        case .community: .nativeMcpFactsBookmarkEntityTypeCommunity
        case .url: .nativeMcpFactsBookmarkEntityTypeUrl
        case .rssFeed: .nativeMcpFactsBookmarkEntityTypeRssFeed
        case .rssFeedItem: .nativeMcpFactsBookmarkEntityTypeRssFeedItem
        case .urlHostname: .nativeMcpFactsBookmarkEntityTypeUrlHostname
        }
    }
}
