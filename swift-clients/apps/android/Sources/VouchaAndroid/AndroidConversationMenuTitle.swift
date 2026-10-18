import Foundation
import VouchaLocalization

func androidConversationMenuTitle(_ title: String, locale: Locale) -> String {
    let trimmed = title.trimmingCharacters(in: .whitespacesAndNewlines)
    let text: UiVerbatimText = trimmed.isEmpty
        ? .message(.nativeSwiftChatNewConversation)
        : .userContent(title)
    return UiMessages.string(text, locale: locale)
}
