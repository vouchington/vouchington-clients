import VouchaAPI

extension NativeChatViewModel {
    func pendingTurn(
        for text: String, providerSelection: NativeChatTitleProviderKind
    ) -> NativeChatPendingLocalTurn? {
        guard let pendingLocalTurn,
              pendingLocalTurn.context.text == text,
              pendingLocalTurn.context.conversationId == selectedConversationId,
              pendingLocalTurn.context.providerSelection == providerSelection
        else { return nil }
        return pendingLocalTurn
    }

    func persistPendingLocalTurnIfAny(
        client: APIClient,
        context: NativeChatDraftSendContext,
        assistantMessageId: String
    ) async throws -> Bool {
        guard let pendingLocalTurn,
              pendingLocalTurn.context.userMessageId == context.userMessageId,
              pendingLocalTurn.context.conversationId == context.conversationId
        else { return false }
        try await persistLocalGenerationResponse(
            client: client,
            context: context,
            assistantMessageId: assistantMessageId,
            response: pendingLocalTurn.response
        )
        return true
    }
}
