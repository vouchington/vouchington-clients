extension ContentView {
    @MainActor
    func discardAndReplaceUnreadableSession() async {
        let capturedAccount = account
        await replaceAccount(capturedAccount) { capturedAccount.discardUnreadableCookieSnapshot() }
    }

    @MainActor
    func replaceAccount(_ capturedAccount: AndroidAccount, operation: @MainActor () async -> Bool) async {
        await accountReplacement.perform(operation: operation, isCurrent: { account === capturedAccount }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }
}
