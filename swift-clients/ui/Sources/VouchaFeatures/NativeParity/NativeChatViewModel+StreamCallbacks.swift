extension NativeChatViewModel {
    func finishStream(
        conversationId: String,
        userMessageId: String,
        createdConversation: Bool
    ) async {
        let didFinishCurrentStream = await MainActor.run { () -> Bool in
            guard streamingConversationId == conversationId else { return false }
            guard streamingUserMessageId == userMessageId else { return false }
            return finishStreaming()
        }
        guard didFinishCurrentStream, createdConversation else { return }
        await generateTitleIfNeeded(conversationId: conversationId)
    }
}
