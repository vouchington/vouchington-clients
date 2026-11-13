import VouchaCore
import VouchaLocalization

/// Maps the stable account-change error codes to localized copy. Unknown codes return nil so the
/// caller keeps its existing generic error presentation.
public enum AccountChangeFailureMessage {
    public enum Context: Sendable {
        /// Removing an email address, OAuth connection, passkey, or TOTP factor.
        case signInMethodRemoval
        /// Confirming identity again with a linked provider.
        case providerReAuth
    }

    public static func key(for error: Error, context: Context) -> UiMessageKey? {
        guard let error = error as? VouchaError else { return nil }
        if error.isLastSignInMethodRemovalForbidden { return UiMessageKey.sharedSignInMethodRemovalLastMethod }
        if error.isAdministratorMfaRequired { return UiMessageKey.sharedSignInMethodRemovalAdministratorMfa }
        guard error.isMfaReauthRequired else { return nil }
        if context == .providerReAuth, error.apiStatusCode == 403 {
            return UiMessageKey.sharedReauthConfirmationChooseAnotherMethod
        }
        return UiMessageKey.sharedReauthConfirmationExpired
    }

    public static func message(for error: Error, context: Context) -> UiVerbatimText? {
        key(for: error, context: context).map { .message($0) }
    }
}
