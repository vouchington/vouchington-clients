import VouchaFeatures

extension RootView {
    /// Binds the native chat MCP session to the signed-in account and connects it without
    /// blocking launch work. A newer account change cancels the pending attempt, and the
    /// account guard drops an attempt that finished choosing after the account moved on.
    func startNativeChatMCPConnect(for accountId: String?) {
        nativeChatMCPConnectTask?.cancel()
        guard let session = viewModelFactory.nativeChatMCPSession else {
            nativeChatMCPConnectTask = nil
            return
        }
        nativeChatMCPConnectTask = Task {
            guard accountId == viewModelFactory.sessionManager.currentUserId else { return }
            await session.useAccount(accountId)
            guard !Task.isCancelled,
                  accountId == viewModelFactory.sessionManager.currentUserId
            else { return }
            try? await session.connect()
        }
    }
}
