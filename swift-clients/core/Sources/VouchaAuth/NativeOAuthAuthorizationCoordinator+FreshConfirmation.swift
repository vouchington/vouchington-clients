import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension NativeOAuthAuthorizationCoordinator {
    func cancelFreshAccountConfirmation() {
        freshConfirmationEpoch &+= 1
        let hasConfirmationResult = switch result {
        case .confirmationReady, .expired(_, .reauthenticate): true
        default: false
        }
        guard pending?.purpose == .reauthenticate || hasConfirmationResult else { return }
        do {
            try store.discardAuthorization()
            retainedCallbackURL = nil
            synchronizeFromStore()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func discardAuthorizationAfterSuccessfulSignIn() {
        do {
            try store.discardAuthorization()
            retainedCallbackURL = nil
            resultAcknowledgementNeedsRetry = false
            errorMessage = nil
            synchronizeFromStore()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    /// Drops a restored or late fresh-confirmation result when no confirmation view model is waiting for it.
    func discardFreshConfirmationWithoutOwner() {
        guard !hasFreshConfirmationOwner else { return }
        switch result {
        case .confirmationReady, .expired(_, .reauthenticate): cancelFreshAccountConfirmation()
        default: break
        }
    }

    /// True when the user cancelled fresh account confirmation after `epoch` was captured.
    internal func isFreshConfirmationAbandoned(_ pending: PendingNativeOAuthAuthorization, since epoch: Int) -> Bool {
        pending.purpose == .reauthenticate && epoch != freshConfirmationEpoch
    }
}
