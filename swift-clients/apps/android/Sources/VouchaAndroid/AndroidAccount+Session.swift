import VouchaLocalization

extension AndroidAccount {
    func retryCookiePersistence() async {
        cookieStorage.persistCookies()
        await cookieStorage.flushPendingPersistence()
        cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
    }

    func retryCookiePersistenceOrRestoration() async {
        guard !isRestoringSession else { return }
        if cookieStorage.requiresRestorationRetry {
            guard cookieStorage.retryRestoreCookies() else {
                cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
                return
            }
            cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
            await restore()
        } else if isRestoring || sessionRestoreError != nil {
            await restore()
        } else {
            await retryCookiePersistence()
        }
    }

    @discardableResult
    func discardUnreadableCookieSnapshot() async -> Bool {
        let discarded = await cookieStorage.discardUnreadableSnapshot()
        cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
        return discarded
    }

    func restore() async {
        guard !isRestoringSession else { return }
        guard !cookieStorage.requiresRestorationRetry else {
            cookiePersistenceNotice.accept(cookieStorage.persistenceStatus)
            return
        }
        isRestoringSession = true
        isRestoring = true
        sessionRestoreError = nil
        defer { isRestoringSession = false }
        await session.restoreSession()
        if let error = session.lastRefreshErrorMessage {
            sessionRestoreError = .verbatim(error)
        } else {
            isRestoring = false
        }
    }
}
