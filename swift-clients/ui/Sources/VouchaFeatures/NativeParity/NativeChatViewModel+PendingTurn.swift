import VouchaAPI

extension NativeChatViewModel {
    func discardInvalidatedCompletedMCPTurn() -> Bool {
        guard let pendingLocalTurn, let snapshot = pendingLocalTurn.mcpSnapshot,
              !matchesMCPAccount(snapshot) else { return false }
        // Do not turn a completed old-account MCP response into a fresh
        // model generation while the mounted surface changes accounts.
        self.pendingLocalTurn = nil
        draftMessage = ""
        return true
    }

    func clearPendingTurnIfProviderChanged() {
        guard let pendingLocalTurn,
              pendingLocalTurn.mcpSnapshot == nil,
              pendingLocalTurn.context.providerSelection != titleProviderSelection else { return }
        self.pendingLocalTurn = nil
    }

    func pendingTurn(
        for text: String,
        providerSelection: NativeChatTitleProviderKind,
        provider: any NativeChatTitleProviding
    ) -> NativeChatPendingLocalTurn? {
        guard let pendingLocalTurn else { return nil }
        let matchesProvider = pendingLocalTurn.context.providerSelection == providerSelection
            || pendingLocalTurn.mcpSnapshot != nil
        guard pendingLocalTurn.context.text == text,
              pendingLocalTurn.context.conversationId == selectedConversationId,
              matchesProvider
        else { return nil }
        if let snapshot = pendingLocalTurn.mcpSnapshot, !matchesMCPAccount(snapshot) {
            self.pendingLocalTurn = nil
            return nil
        }
        // A completed MCP turn may already have mutated server state. Persist
        // its original response even if the local model selection has changed.
        if pendingLocalTurn.mcpSnapshot == nil, case .openAICompatible = providerSelection {
            guard let currentModel = provider.retryModelIdentity,
                  pendingLocalTurn.response.modelName == currentModel
            else {
                self.pendingLocalTurn = nil
                return nil
            }
        }
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
        if let snapshot = pendingLocalTurn.mcpSnapshot, !matchesMCPAccount(snapshot) {
            self.pendingLocalTurn = nil
            abortStreaming()
            return true
        }
        try await persistLocalGenerationResponse(
            client: client,
            context: context,
            assistantMessageId: assistantMessageId,
            response: pendingLocalTurn.response,
            mcpSnapshot: pendingLocalTurn.mcpSnapshot
        )
        return true
    }
}
