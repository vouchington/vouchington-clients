import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

/// A localized label beside a localized or verbatim value.
struct NativeChatFactRow: View {
    let label: UiMessageKey
    let value: UiVerbatimText
    let locale: Locale

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
            Text(UiMessages.string(label, locale: locale))
                .foregroundStyle(Colors.secondaryLabel)
            Text(verbatim: UiMessages.string(value, locale: locale))
        }
        .accessibilityElement(children: .combine)
    }
}
