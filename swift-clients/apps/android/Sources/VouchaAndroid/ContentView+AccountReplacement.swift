extension ContentView {
    @MainActor
    func signOutAndReplaceAccount() async {
        let capturedAccount = account
        await replaceAccount(capturedAccount) { await capturedAccount.signOut() }
    }

    @MainActor
    func discardAndReplaceUnreadableSession() async {
        let capturedAccount = account
        await replaceAccount(capturedAccount) { capturedAccount.discardUnreadableCookieSnapshot() }
    }

    @MainActor
    private func replaceAccount(_ capturedAccount: AndroidAccount, operation: @MainActor () async -> Bool) async {
        await accountReplacement.perform(operation: operation, isCurrent: { account === capturedAccount }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }
}
