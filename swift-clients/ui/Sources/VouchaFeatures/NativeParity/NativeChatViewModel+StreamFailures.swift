import VouchaLocalization

extension NativeChatViewModel {
    func failStream(
        conversationId: String,
        userMessageId: String,
        message: UiVerbatimText,
        createdConversation: Bool
    ) async {
        await MainActor.run {
            guard streamingConversationId == conversationId else { return }
            guard streamingUserMessageId == userMessageId else { return }
            if streamingAssistantMessageId == nil, createdConversation {
                discardConversationMessage(id: userMessageId)
            }
            failStreaming(message: message)
        }
    }
}
