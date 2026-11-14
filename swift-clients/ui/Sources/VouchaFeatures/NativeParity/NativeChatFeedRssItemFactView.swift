import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatFeedRssItemFactView: View {
    let itemIds: [String]
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftRouteSurfaceRssItem, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            if itemIds.isEmpty {
                Text(UiMessages.string(.nativeSwiftRouteSurfaceNoResults, locale: locale))
            } else {
                ForEach(itemIds, id: \.self) { itemId in
                    Text(UiMessages.string(itemLabel(itemId), locale: locale))
                        .accessibilityElement(children: .combine)
                }
            }
        }
        .accessibilityIdentifier("native-chat-feed-rss-item-fact")
    }

    private func itemLabel(_ itemId: String) -> UiMessage {
        UiMessage(.nativeSwiftIntegrityRssItemId, textParameters: ["id": .protocolValue(itemId)])
    }
}
