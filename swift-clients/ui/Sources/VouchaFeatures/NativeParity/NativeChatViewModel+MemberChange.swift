extension NativeChatViewModel {
    func resetForMemberChange() {
        // The mounted surface can outlive an account change. A completed
        // MCP turn must never become a fresh send for the next member.
        abortStreaming()
        expireMCPReceipts()
        pendingLocalTurn = nil
        draftMessage = ""
        selectedConversationId = nil
        messages = []
        conversations = []
        detailPageInfo = nil
        conversationTitleDraft = ""
        createdConversationIds = []
        loadedConversationDetailId = nil
        listErrorMessage = nil
        detailErrorMessage = nil
        streamErrorMessage = nil
        isLoadingList = false
        isLoadingDetail = false
        isLoadingOlderMessages = false
        selectionRevision += 1
        olderMessagesLoadRevision += 1
        activeConversationDetailLoadRevision += 1
        activeConversationListLoadRevision += 1
    }
}
