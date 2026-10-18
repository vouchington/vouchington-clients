extension ContentView {
    @MainActor
    func signOutAndReplaceAccount() async {
        let capturedAccount = account
        await accountReplacement.perform(operation: {
            guard await viewModel.prepareForAccountExit() else { return false }
            let signedOut = await capturedAccount.signOut()
            if !signedOut { viewModel.cancelAccountExit() }
            return signedOut
        }, isCurrent: {
            account === capturedAccount
        }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }

    @MainActor
    func discardAndReplaceUnreadableSession() async {
        let capturedAccount = account
        await accountReplacement.perform(operation: {
            let hasBoundOwner = viewModel.currentAccountUserID != nil
            if hasBoundOwner, await !(viewModel.prepareForAccountExit()) { return false }
            let discarded = await capturedAccount.discardUnreadableCookieSnapshot()
            if !discarded, hasBoundOwner { viewModel.cancelAccountExit() }
            return discarded
        }, isCurrent: {
            account === capturedAccount
        }, replace: {
            account = AndroidAccount(config: capturedAccount.config)
            await account.restore()
        })
    }
}
