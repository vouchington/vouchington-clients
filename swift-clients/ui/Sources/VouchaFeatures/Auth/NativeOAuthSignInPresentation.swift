import Observation
import VouchaLocalization

@Observable
@MainActor
final class NativeOAuthSignInPresentation {
    var error: UiVerbatimText?
    var isPresentingExpiration = false
}
