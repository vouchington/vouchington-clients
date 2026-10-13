extension NativeChatViewModel {
    func loadInitialConversation(_ initialConversationId: String?) async {
        let memberId = currentMemberId
        let initialSelectionRevision = selectionRevision
        await loadConversations(reset: true)
        guard currentMemberId == memberId, selectionRevision == initialSelectionRevision,
              !Task.isCancelled else { return }
        if let initialConversationId {
            await selectConversation(id: initialConversationId)
        }
    }
}
