import Foundation

extension AndroidSessionCookieStorage {
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
