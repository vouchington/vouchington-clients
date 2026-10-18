import Foundation
import VouchaCore
#if !canImport(Darwin)
    import FoundationNetworking
#endif

/// Keeps URLSession cookies in its ephemeral jar and persists snapshots only through SkipKeychain.
final class AndroidSessionCookieStorage: @unchecked Sendable {

    let sessionConfiguration: URLSessionConfiguration
    let cookieStorage: HTTPCookieStorage
    private let secrets: any AndroidEndpointSecretStoring
    private let key: String
    private let snapshotRecovery: AndroidCookieSnapshotRecovery
    private let queue = DispatchQueue(label: "ai.voucha.android.session-cookies")

    private var persistenceEnabled = true
    private var restorationBlocked = false
    private var persistenceRevision = 0
    private var persistenceErrorMessage: String?
    private var observer: (@Sendable (PersistenceStatus) -> Void)?

    init(
        secrets: any AndroidEndpointSecretStoring = SkipKeychainEndpointSecretStore(),
        baseURL: URL = AppConfig.shared.baseURL
    ) {
        guard let key = AndroidSessionCookieNamespace.key(for: baseURL) else {
            preconditionFailure("Android authentication requires an HTTP(S) API origin")
        }
        let configuration = URLSessionConfiguration.ephemeral
        guard let cookieStorage = configuration.httpCookieStorage else {
            preconditionFailure("Android authentication requires URLSession's ephemeral cookie store")
        }
        sessionConfiguration = configuration
        self.cookieStorage = cookieStorage
        self.secrets = secrets
        self.key = key
        snapshotRecovery = AndroidCookieSnapshotRecovery(secrets: secrets, key: key, cookieStorage: cookieStorage)
        restoreCookies()
    }

    var persistenceStatus: PersistenceStatus {
        queue.sync { statusHoldingQueue() }
    }

    var requiresRestorationRetry: Bool {
        queue.sync { restorationBlocked }
    }

    func observePersistence(_ observer: @escaping @Sendable (PersistenceStatus) -> Void) {
        let status = queue.sync {
            self.observer = observer
            return statusHoldingQueue()
        }
        observer(status)
    }

    func persistCookies() {
        let result = queue.sync { () -> PersistenceStatus? in
            guard persistenceEnabled, !restorationBlocked else { return nil }
            do {
                let records = (cookieStorage.cookies ?? []).compactMap(CookieRecord.init(cookie:))
                    .filter { $0.expires.map { $0 > Date() } ?? true }
                let data = try JSONEncoder().encode(records)
                guard let json = String(data: data, encoding: .utf8) else {
                    throw CocoaError(.fileWriteInapplicableStringEncoding)
                }
                try secrets.set(json, forKey: key)
                persistenceErrorMessage = nil
            } catch {
                persistenceErrorMessage = error.localizedDescription
            }
            persistenceRevision += 1
            return statusHoldingQueue()
        }
        if let result { notify(result) }
    }

    /// Permanently fences this client's callbacks before logout or account replacement.
    func disablePersistenceWrites() {
        let status = queue.sync { disableHoldingQueue() }
        notify(status)
    }

    func clearPersistedCookies() throws {
        let status = try queue.sync {
            try secrets.removeValue(forKey: key)
            return disableHoldingQueue()
        }
        notify(status)
    }

    @discardableResult
    func retryRestoreCookies() -> Bool {
        let result = queue.sync { () -> (Bool, PersistenceStatus)? in
            guard restorationBlocked else {
                return (persistenceEnabled, statusHoldingQueue())
            }
            do {
                try snapshotRecovery.loadPersistedCookies()
                restorationBlocked = false
                persistenceEnabled = true
                persistenceErrorMessage = nil
            } catch {
                persistenceErrorMessage = error.localizedDescription
            }
            persistenceRevision += 1
            return (!restorationBlocked, statusHoldingQueue())
        }
        guard let result else { return true }
        notify(result.1)
        return result.0
    }

    @discardableResult
    func discardUnreadableSnapshot() -> Bool {
        let result = queue.sync { () -> (Bool, PersistenceStatus) in
            guard restorationBlocked, snapshotRecovery.unreadableSnapshotCanBeDiscarded else {
                return (false, statusHoldingQueue())
            }
            do {
                try snapshotRecovery.discardUnreadableSnapshot()
            } catch {
                persistenceErrorMessage = error.localizedDescription
                persistenceRevision += 1
                return (false, statusHoldingQueue())
            }
            return (true, disableHoldingQueue())
        }
        notify(result.1)
        return result.0
    }

    private func disableHoldingQueue() -> PersistenceStatus {
        persistenceEnabled = false
        restorationBlocked = false
        persistenceErrorMessage = nil
        persistenceRevision += 1
        return statusHoldingQueue()
    }

    private func statusHoldingQueue() -> PersistenceStatus {
        PersistenceStatus(
            revision: persistenceRevision,
            errorMessage: persistenceErrorMessage,
            enabled: persistenceEnabled,
            restorationBlocked: restorationBlocked,
            unreadableSnapshotCanBeDiscarded: restorationBlocked && snapshotRecovery.unreadableSnapshotCanBeDiscarded
        )
    }

    private func notify(_ status: PersistenceStatus) {
        let observer = queue.sync { self.observer }
        observer?(status)
    }

    private func restoreCookies() {
        do {
            try snapshotRecovery.loadPersistedCookies()
        } catch {
            restorationBlocked = true
            persistenceEnabled = false
            persistenceErrorMessage = error.localizedDescription
            persistenceRevision += 1
        }
    }

}
