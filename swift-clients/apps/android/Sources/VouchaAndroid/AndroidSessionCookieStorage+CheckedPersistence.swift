import Foundation

extension AndroidSessionCookieStorage {
    /// Clearing is allowed after sign-out has permanently fenced the old client's writes.
    func persistClearedCookiesChecked() throws {
        try queue.sync { try secrets.removeValue(forKey: key) }
    }

    /// Called while the shared session coordinator excludes other cookie commits.
    func persistCookiesChecked() throws {
        persistCookies()
        queue.sync {}
        let status = persistenceStatus
        guard status.enabled, !status.restorationBlocked, status.errorMessage == nil else {
            throw CocoaError(.fileWriteUnknown)
        }
    }

    /// Wait for queued cookie writes without running their disk I/O on the caller's actor.
    func flushPendingPersistence() async {
        await Task.detached {
            self.queue.sync {}
            self.notificationQueue.sync {}
        }.value
    }

    /// A successful identity fetch must not publish signed-in state before its cookies are stored.
    func flushCheckedPersistence() async throws {
        await flushPendingPersistence()
        let status = persistenceStatus
        guard status.enabled, !status.restorationBlocked, status.errorMessage == nil else {
            throw AndroidCookiePersistenceError.unavailable(status.errorMessage)
        }
    }

}

private enum AndroidCookiePersistenceError: LocalizedError {
    case unavailable(String?)

    var errorDescription: String? {
        switch self {
        case let .unavailable(message): message ?? CocoaError(.fileWriteUnknown).localizedDescription
        }
    }
}
