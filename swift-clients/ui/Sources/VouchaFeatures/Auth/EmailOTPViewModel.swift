import Foundation
import Observation
import VouchaAuth
import VouchaCore
import VouchaLocalization
import VouchaModels

/// The step within the email OTP sign-in flow.
public enum OTPStep: Equatable {
    case enterEmail
    case enterCode
    case mfaChallenge(loginAttemptId: String)
}

@Observable
@MainActor
public final class EmailOTPViewModel {
    public var step: OTPStep = .enterEmail
    public var email: String = ""
    public var code: String = ""
    public var turnstileToken: String?
    public var uiLocale: String?
    public var isLoading: Bool = false
    public var errorMessage: UiVerbatimText?

    public private(set) var didSucceed: Bool = false
    /// Stable VM for the MFA challenge step; set once when verifyCode succeeds with MFA required.
    public private(set) var mfaChallengeVM: MFAChallengeViewModel?
    private let signInService: SignInService

    /// Creates a view model backed by the given sign-in service.
    public init(
        signInService: SignInService,
        uiLocale: UiLocale = UiLocaleResolver.resolve(savedUiLocale: nil),
        initialEmail: String? = nil,
        initialCode: String? = nil
    ) {
        self.signInService = signInService
        self.uiLocale = uiLocale.rawValue
        email = initialEmail ?? ""
        code = initialCode ?? ""
        if !email.isEmpty, !code.isEmpty {
            step = .enterCode
        }
    }

    /// Whether `requestCode()` can proceed: either a Turnstile token is present, or App Attest
    /// can stand in for it. UI layers use this to enable the Send Code button.
    public var canRequestCode: Bool {
        !email.isEmpty && !isLoading && (turnstileToken != nil || signInService.canBypassTurnstileWithAppAttest)
    }

    /// Requests an OTP code for the entered email address.
    public func requestCode() async {
        guard !email.isEmpty, !isLoading else { return }
        guard turnstileToken != nil || signInService.canBypassTurnstileWithAppAttest else {
            errorMessage = localizedVerificationRequired
            return
        }
        isLoading = true
        errorMessage = nil
        do {
            try await signInService.requestEmailOTP(
                email: email,
                turnstileToken: turnstileToken,
                uiLocale: uiLocale
            )
            step = .enterCode
        } catch let error as VouchaError where error.isTurnstileTokenRequired {
            // The App Attest bypass looked available (`canBypassTurnstileWithAppAttest`) but
            // failed or was rejected server-side. Show the same prompt as the pre-flight gate
            // above, matching how the post-compose/topic-recommendation flows move into a
            // "verification required" state rather than surfacing the raw precondition code.
            errorMessage = localizedVerificationRequired
        } catch {
            errorMessage = .verbatim(error.localizedDescription)
        }
        isLoading = false
    }

    /// Verifies the entered OTP code and advances to MFA if required.
    public func verifyCode() async {
        guard !code.isEmpty, !isLoading else { return }
        isLoading = true
        errorMessage = nil
        do {
            if let challenge = try await signInService.verifyEmailOTP(email: email, code: code) {
                mfaChallengeVM = MFAChallengeViewModel(
                    loginAttemptId: challenge.loginAttemptId,
                    signInService: signInService
                )
                step = .mfaChallenge(loginAttemptId: challenge.loginAttemptId)
            } else {
                didSucceed = true
            }
        } catch {
            errorMessage = .verbatim(error.localizedDescription)
        }
        isLoading = false
    }

    /// Called when MFA TOTP verification completes successfully.
    public func didCompleteMFA() {
        didSucceed = true
    }

    static func defaultUiLocale(preferredLanguages: [String] = Locale.preferredLanguages) -> String? {
        UiLocaleResolver.resolve(savedUiLocale: nil, preferredLanguages: preferredLanguages).rawValue
    }

    private var localizedVerificationRequired: UiVerbatimText {
        .message(.nativeAuthVerificationTokenRequired)
    }

    static func normalizeUiLocale(_ tag: String) -> String? {
        UiLocale.normalized(tag)?.rawValue
    }

    static func normalizeUiLocale(_ tag: String, supportedUiLocales: Set<String>) -> String? {
        let normalized = tag.trimmingCharacters(in: .whitespacesAndNewlines)
            .replacingOccurrences(of: "_", with: "-")
            .lowercased()
        guard !normalized.isEmpty else { return nil }
        if supportedUiLocales.contains(normalized) {
            return normalized
        }
        guard let base = normalized.split(separator: "-").first.map(String.init), !base.isEmpty else {
            return nil
        }
        return supportedUiLocales.contains(base) ? base : nil
    }
}
