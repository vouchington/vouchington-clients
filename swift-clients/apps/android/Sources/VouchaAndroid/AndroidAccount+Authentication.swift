import Foundation
import VouchaLocalization

extension AndroidAccount {
    func requestOTP(locale: Locale = .current) async {
        guard !isAuthenticating, turnstileToken != nil else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            let token = turnstileToken
            defer {
                turnstileToken = nil
                turnstileChallengeID = UUID()
            }
            try await signIn.requestEmailOTP(
                email: email,
                turnstileToken: token,
                uiLocale: UiLocaleResolver.resolve(savedUiLocale: locale.identifier, preferredLanguages: []).rawValue
            )
            otpRequested = true
        } catch {
            turnstileToken = nil
            authError = .verbatim(error.localizedDescription)
        }
    }

    func turnstileTokenDidChange(_ token: String?, challengeID: UUID? = nil, locale _: Locale = .current) {
        guard !isAuthenticating, challengeID == nil || challengeID == turnstileChallengeID else { return }
        turnstileToken = token
        authError = token == nil ? .message(.nativeAuthVerificationTokenRequired) : nil
    }

    func retryTurnstileChallenge() {
        guard !isAuthenticating else { return }
        turnstileToken = nil
        turnstileChallengeID = UUID()
        authError = nil
    }

    func verifyOTP() async {
        guard !isAuthenticating else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            if let challenge = try await signIn.verifyEmailOTP(email: email, code: otp) {
                loginAttemptID = challenge.loginAttemptId
            }
        } catch {
            handleVerificationFailure(error)
        }
    }

    func verifyMFA() async {
        guard let loginAttemptID, !isAuthenticating else { return }
        isAuthenticating = true
        authError = nil
        defer { isAuthenticating = false }
        do {
            try await signIn.verifyMFATOTP(loginAttemptId: loginAttemptID, code: totp)
            self.loginAttemptID = nil
        } catch {
            handleVerificationFailure(error)
        }
    }

    private func handleVerificationFailure(_ error: Error) {
        guard let refreshError = session.lastRefreshErrorMessage else {
            authError = .verbatim(error.localizedDescription)
            return
        }
        otpRequested = false
        isRestoring = true
        sessionRestoreError = .verbatim(refreshError)
        authError = nil
        loginAttemptID = nil
        otp = ""
        totp = ""
    }

    @discardableResult
    func restartSignIn() -> Bool {
        guard !isAuthenticating else { return false }
        loginAttemptID = nil
        otpRequested = false
        otp = ""
        totp = ""
        turnstileToken = nil
        turnstileChallengeID = UUID()
        authError = nil
        return true
    }
}
