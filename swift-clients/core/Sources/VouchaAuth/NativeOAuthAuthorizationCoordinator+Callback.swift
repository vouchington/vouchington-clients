import Foundation

public extension NativeOAuthAuthorizationCoordinator {
    var canRecoverFailedFinalization: Bool {
        (
            retainedCallbackURL != nil ||
                pending?.isFinalizing == true ||
                resultAcknowledgementNeedsRetry ||
                secureStateReadUnavailable
        ) &&
            errorMessage != nil &&
            !isWorking
    }

    func retryFailedFinalization() async {
        guard canRecoverFailedFinalization else { return }
        if resultAcknowledgementNeedsRetry {
            acknowledgeResult()
        } else if let retainedCallbackURL {
            await claimAndFinalizeCallback(retainedCallbackURL)
        } else if pending != nil {
            await resumePendingAuthorization()
        } else if secureStateReadUnavailable {
            await resumePendingAuthorization()
        }
    }

    func handleIncomingURL(_ url: URL) -> Bool {
        guard store.isCallbackURL(url) else { return false }
        do {
            guard let claimed = try store.claimCallback(for: url, now: now()) else {
                synchronizeFromStore()
                return true
            }
            retainedCallbackURL = nil
            synchronizeFromStore()
            Task { await finalize(claimed) }
            return true
        } catch {
            retainedCallbackURL = url
            errorMessage = error.localizedDescription
            return true
        }
    }
}

private extension NativeOAuthAuthorizationCoordinator {
    func claimAndFinalizeCallback(_ url: URL) async {
        do {
            guard let claimed = try store.claimCallback(for: url, now: now()) else {
                retainedCallbackURL = nil
                synchronizeFromStore()
                return
            }
            retainedCallbackURL = nil
            synchronizeFromStore()
            await finalize(claimed)
        } catch {
            retainedCallbackURL = url
            errorMessage = error.localizedDescription
        }
    }
}
