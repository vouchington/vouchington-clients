import Observation
import VouchaCore
import VouchaLocalization

public enum ReAuthConfirmationState: Equatable, Sendable {
    case selectingMethod
    case expired
    case canceled
    /// The provider proof belongs to another session; only a different method can recover.
    case rejected
}

/// Outcome of a provider re-authentication attempt and its recovery: confirm again after an
/// expiry, or return to method selection after a cancel or a rejected (other-session) proof.
@Observable
@MainActor
public final class ReAuthConfirmationModel {
    public internal(set) var state: ReAuthConfirmationState = .selectingMethod
    /// Set when the user is sent back to method selection after a failed attempt.
    public internal(set) var prompt: UiMessage?
    /// Failures without a confirmation code keep the caller's generic presentation.
    public internal(set) var unrecognizedError: Error?

    public init() {}

    public var message: UiMessage? {
        switch state {
        case .selectingMethod: nil
        case .expired: UiMessage(.sharedReauthConfirmationExpired)
        case .canceled: UiMessage(.sharedReauthConfirmationCanceled)
        case .rejected: UiMessage(.sharedReauthConfirmationChooseAnotherMethod)
        }
    }

    public func recordFailure(_ error: Error) {
        unrecognizedError = nil
        guard let apiError = error as? VouchaError, apiError.isMfaReauthRequired else {
            unrecognizedError = error
            return
        }
        state = apiError.apiStatusCode == 403 ? .rejected : .expired
    }

    public func recordCancellation() {
        unrecognizedError = nil
        state = .canceled
    }

    /// Returns to method selection with the choose-another-method prompt.
    public func chooseAnotherMethod() {
        unrecognizedError = nil
        state = .selectingMethod
        prompt = UiMessage(.sharedReauthConfirmationChooseAnotherMethod)
    }

    public func beginAttempt() {
        unrecognizedError = nil
        state = .selectingMethod
        prompt = nil
    }
}
