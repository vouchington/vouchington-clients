extension ContentView {
    @MainActor
    func signOutAndReplaceAccount() async {
        let capturedAccount = account
        await accountReplacement.perform(operation: { await capturedAccount.signOut() }, isCurrent: {
            account === capturedAccount
        }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }

    @MainActor
    func discardAndReplaceUnreadableSession() async {
        let capturedAccount = account
        await accountReplacement.perform(operation: { capturedAccount.discardUnreadableCookieSnapshot() }, isCurrent: {
            account === capturedAccount
        }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }
}
