extension DirectMessagesSurface {
    var activeConversationId: String? {
        Self.resolveActiveConversationId(
            isShowingInbox: isShowingInbox,
            isShowingNewMessage: isShowingNewMessage,
            selectedConversationId: selectedConversationId,
            viewModelSelectedConversationId: viewModel.selectedConversationId,
            initialConversationId: initialConversationId
        )
    }

    var loadTaskId: String {
        if isShowingInbox {
            return "inbox"
        }
        if isShowingNewMessage {
            return "new"
        }
        return activeConversationId ?? "inbox"
    }

    static func resolveActiveConversationId(
        isShowingInbox: Bool,
        isShowingNewMessage: Bool,
        selectedConversationId: String?,
        viewModelSelectedConversationId: String?,
        initialConversationId: String?
    ) -> String? {
        if isShowingInbox {
            return nil
        }
        if isShowingNewMessage, selectedConversationId == nil, viewModelSelectedConversationId == nil {
            return nil
        }
        return selectedConversationId
            ?? viewModelSelectedConversationId
            ?? initialConversationId.flatMap { $0 == "new" ? nil : $0 }
    }
}
