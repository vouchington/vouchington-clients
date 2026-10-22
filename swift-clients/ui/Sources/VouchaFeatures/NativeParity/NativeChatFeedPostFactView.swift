import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatFeedPostFactView: View {
    let postIds: [String]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftRouteSurfacePosts, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if postIds.isEmpty {
                Text(UiMessages.string(.nativeSwiftEmptyStateNoPosts, locale: locale))
            } else {
                ForEach(postIds, id: \.self) { postId in
                    HStack(spacing: Spacing.xs) {
                        Text(UiMessages.string(.nativeSwiftCommunitiesPostId, locale: locale))
                        Text(verbatim: UiMessages.string(.protocolValue(postId), locale: locale))
                    }
                    .accessibilityElement(children: .combine)
                }
            }
        }
        .accessibilityIdentifier("native-chat-feed-post-fact")
    }
}
