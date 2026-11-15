import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeChatPostSummaryFactView: View {
    let posts: [NativeChatPostSummaryFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftRouteSurfacePosts, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if posts.isEmpty {
                Text(UiMessages.string(.nativeSwiftEmptyStateNoPosts, locale: locale))
            } else {
                ForEach(posts) { post in
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(verbatim: UiMessages.string(.userContent(post.title), locale: locale))
                        HackerNewsReferencesView(references: post.references, expanded: post.references?.stories != nil)
                    }
                }
            }
        }
        .accessibilityIdentifier("native-chat-post-summary-fact")
    }
}

struct NativeChatRssItemSummaryFactView: View {
    let items: [NativeChatRssItemSummaryFact]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftRouteSurfaceRssItem, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if items.isEmpty {
                Text(UiMessages.string(.nativeSwiftRouteSurfaceNoResults, locale: locale))
            } else {
                ForEach(items) { item in
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(verbatim: UiMessages.string(.userContent(item.title), locale: locale))
                        Text(verbatim: UiMessages.string(.userContent(item.feedTitle), locale: locale))
                            .foregroundStyle(Colors.secondaryLabel)
                        HackerNewsReferencesView(references: item.references, expanded: item.references?.stories != nil)
                    }
                }
            }
        }
        .accessibilityIdentifier("native-chat-rss-item-summary-fact")
    }
}
