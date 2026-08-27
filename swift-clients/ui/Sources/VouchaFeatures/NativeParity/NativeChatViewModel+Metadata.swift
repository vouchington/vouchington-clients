import VouchaModels

extension NativeChatViewModel {
    func hydrateConversationMetadataIfNeeded(id: String, revision: Int) async {
        guard isCurrentConversationDetailLoad(id: id, revision: revision) else { return }
        guard let conversation = conversations.first(where: { $0.id == id }) else { return }
        conversationTitleDraft = conversation.title
    }
}
