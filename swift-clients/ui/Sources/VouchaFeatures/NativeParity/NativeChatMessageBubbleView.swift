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

            Text(message.content.isEmpty ? " " : message.content)
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(Spacing.sm)
                .background(
                    RoundedRectangle(cornerRadius: 8, style: .continuous)
                        .fill(message.role == .user ? Colors.primary.opacity(0.12) : Colors.background)
                )

            if !message.toolCalls.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text(UiMessages.string(.nativeSwiftChatMessageBubbleToolCalls, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    ForEach(Array(message.toolCalls.enumerated()), id: \.offset) { _, toolCall in
                        Text(verbatim: UiMessages.string(
                            .protocolValue("\(toolCall.name): \(toolCall.arguments)"),
                            locale: nativeUiLocale
                        ))
                        .font(Typography.caption)
                    }
                }
            }

            if !message.toolResults.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text(UiMessages.string(.nativeSwiftChatMessageBubbleToolResults, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    ForEach(message.toolResults) { result in
                        Text(verbatim: UiMessages.string(
                            .joined([
                                .verbatim(result.toolCallId),
                                result.displayText
                            ], separator: ": "),
                            locale: nativeUiLocale
                        ))
                        .font(Typography.caption)
                    }
                }
            }

            if !message.subagentSteps.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text(UiMessages.string(.nativeSwiftChatMessageBubbleSubagentSteps, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    ForEach(Array(message.subagentSteps.enumerated()), id: \.offset) { _, step in
                        Text(verbatim: UiMessages.string(
                            .protocolValue("\(step.agentName) • \(step.toolName)"),
                            locale: nativeUiLocale
                        ))
                        .font(Typography.caption)
                    }
                }
            }

            if !message.subagentTextChunks.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    Text(UiMessages.string(.nativeSwiftChatMessageBubbleSubagentText, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                    ForEach(Array(message.subagentTextChunks.enumerated()), id: \.offset) { _, chunk in
                        Text(verbatim: UiMessages.string(
                            .protocolValue("\(chunk.agentName): \(chunk.content)"),
                            locale: nativeUiLocale
                        ))
                        .font(Typography.caption)
                    }
                }
            }

            if let error = message.error {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }
}
