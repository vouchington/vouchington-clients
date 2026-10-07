import VouchaLocalization
import VouchaModels

extension NativeChatViewModel {
    func updateConversation(id: String, mutate: (inout ChatConversation) -> Void) {
        guard let index = conversations.firstIndex(where: { $0.id == id }) else { return }
        var conversation = conversations[index]
        mutate(&conversation)
        conversations[index] = conversation
    }

    func updateConversationMessage(
        id: String,
        mutate: (inout NativeChatTimelineMessage) -> Void
    ) {
        guard let index = messages.firstIndex(where: { $0.id == id }) else { return }
        var message = messages[index]
        mutate(&message)
        messages[index] = message
    }

    func discardConversationMessage(id: String) {
        messages.removeAll { $0.id == id }
    }

    func makeTimelineMessage(from message: ChatMessage) -> NativeChatTimelineMessage {
        let role: NativeChatTimelineMessage.Role = message.content.role == "assistant" ? .assistant : .user
        return NativeChatTimelineMessage(
            id: message.id,
            role: role,
            content: message.content.displayText,
            isStreaming: false,
            error: message.presentationError
        )
    }
}
