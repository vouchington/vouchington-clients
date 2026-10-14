extension NativeChatAssistantResponse {
    func persistenceSnapshot(
        _ captured: NativeChatMCPAgentSession.ConnectedSnapshot?,
        providerSelection: NativeChatTitleProviderKind
    ) -> NativeChatMCPAgentSession.ConnectedSnapshot? {
        if case .appleFoundationModels = providerSelection, !admittedMCPCall { return nil }
        return captured
    }
}
