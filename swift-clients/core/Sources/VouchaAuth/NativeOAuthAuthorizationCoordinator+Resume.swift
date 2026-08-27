import VouchaCore

public extension NativeOAuthAuthorizationCoordinator {
    func resumePendingAuthorization() async {
        do {
            let snapshot = try store.snapshot(now: now())
            apply(snapshot)
            switch snapshot.pendingStatus {
            case let .active(pending) where pending.isFinalizing:
                clearSecureStateReadError()
                await finalize(pending)
            case let .expired(pending):
                try store.record(.expired(
                    provider: pending.provider,
                    purpose: pending.purpose
                ))
                synchronizeFromStore()
            case .active, .none:
                clearSecureStateReadError()
            }
        } catch {
            secureStateReadUnavailable = true
            errorMessage = error.localizedDescription
        }
    }
}
