import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeChatMessageBubbleView: View {
    @Environment(\.locale)
    var nativeUiLocale
    let message: NativeChatTimelineMessage

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack {
                Text(UiMessages.string(
                    message.role == .user ? .nativeSwiftChatYou : .nativeSwiftChatAssistant,
                    locale: nativeUiLocale
                ))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
                if message.isStreaming {
                    ProgressView()
                        .controlSize(.small)
                }
                Spacer(minLength: 0)
            }

            if !message.content.isEmpty || message.error == nil {
                Text(message.content.isEmpty ? " " : message.content)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(Spacing.sm)
                    .background(
                        RoundedRectangle(cornerRadius: 8, style: .continuous)
                            .fill(message.role == .user ? Colors.primary.opacity(0.12) : Colors.background)
                    )
            }

            if let error = message.error {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }
}
