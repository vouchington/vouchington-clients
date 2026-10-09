import Foundation

struct NativeChatDraftSendContext {
    let conversationId: String
    let text: String
    let userMessageId: String
    let assistantMessageId: String?
    let createdConversation: Bool
    let providerSelection: NativeChatTitleProviderKind

    init(
        conversationId: String,
        text: String,
        userMessageId: String,
        assistantMessageId: String? = nil,
        createdConversation: Bool,
        providerSelection: NativeChatTitleProviderKind = .appleFoundationModels
    ) {
        self.conversationId = conversationId
        self.text = text
        self.userMessageId = userMessageId
        self.assistantMessageId = assistantMessageId
        self.createdConversation = createdConversation
        self.providerSelection = providerSelection
    }
}
