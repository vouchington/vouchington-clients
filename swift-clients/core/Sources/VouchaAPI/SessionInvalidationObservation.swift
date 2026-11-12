import Foundation

/// Keeps a shared cookie-jar invalidation subscription alive until its owner is released.
public final class SessionInvalidationObservation: @unchecked Sendable {
    private let cancel: @Sendable () -> Void

    init(cancel: @escaping @Sendable () -> Void) {
        self.cancel = cancel
    }

    deinit { cancel() }
}

extension SessionCookieCoordinator {
    func observeInvalidation(_ observer: @escaping @Sendable (UInt64, UInt64) -> Void)
        -> SessionInvalidationObservation {
        let id = UUID()
        lock.withLock { invalidationObservers[id] = observer }
        return SessionInvalidationObservation { [weak self] in
            self?.removeObserver(id)
        }
    }

    private func removeObserver(_ id: UUID) {
        _ = lock.withLock { invalidationObservers.removeValue(forKey: id) }
    }

    func emitInvalidation() {
        for observer in invalidationObservers.values {
            observer(epoch, revision)
        }
    }
}

public extension APIClient {
    nonisolated func observeSessionInvalidation(
        _ observer: @escaping @Sendable (UInt64, UInt64) -> Void
    ) -> SessionInvalidationObservation {
        cookieCoordinator.observeInvalidation(observer)
    }

    nonisolated var sessionRevision: UInt64 {
        cookieCoordinator.stamp.revision
    }
}
