import VouchaCore
import VouchaLocalization

extension SignInView {
    @MainActor
    func completeSuccessfulSignIn() {
        nativeOAuthAuthorizationCoordinator?.discardAuthorizationAfterSuccessfulSignIn()
        onSuccess()
    }

    @MainActor
    func beginNativeOAuthSignIn(_ provider: NativeOAuthProvider) async {
        guard let nativeOAuthAuthorizationCoordinator else { return }
        await nativeOAuthAuthorizationCoordinator.begin(
            provider: provider,
            purpose: .authenticate,
            openAuthorizationURL: { openURL($0) }
        )
        if let error = nativeOAuthAuthorizationCoordinator.errorMessage {
            oauthPresentation.error = .verbatim(error)
        }
    }

    @MainActor
    func consumeNativeOAuthResult(_ result: NativeOAuthAuthorizationResult?) {
        guard let result else { return }
        switch result {
        case .authenticated:
            nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
            completeSuccessfulSignIn()
            dismiss()
        case let .mfaRequired(_, loginAttemptId):
            mfaVM = MFAChallengeViewModel(
                loginAttemptId: loginAttemptId,
                signInService: signInService,
                origin: .brokerOAuth
            )
            showingMFAChallenge = true
        case .connected:
            break
        case .expired:
            oauthPresentation.isPresentingExpiration = true
            oauthPresentation.error = .message(.nativeSwiftSettingsOauthExpired)
        }
    }

    @MainActor
    func synchronizeNativeOAuthError() {
        guard !oauthPresentation.isPresentingExpiration,
              let coordinator = nativeOAuthAuthorizationCoordinator
        else { return }
        if coordinator.canRecoverFailedFinalization,
           let error = coordinator.errorMessage {
            oauthPresentation.error = .verbatim(error)
        } else if let error = coordinator.capabilityErrorMessage {
            oauthPresentation.error = .verbatim(error)
        }
    }

    @MainActor
    func retryNativeOAuthFinalization() async {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        oauthPresentation.error = nil
        await coordinator.retryFailedFinalization()
        synchronizeNativeOAuthError()
    }

    @MainActor
    func retryNativeOAuthCapabilityLoading() async {
        guard let coordinator = nativeOAuthAuthorizationCoordinator else { return }
        oauthPresentation.error = nil
        await coordinator.loadCapabilities()
        synchronizeNativeOAuthError()
    }

    @MainActor
    func cancelNativeOAuthAuthorization() {
        nativeOAuthAuthorizationCoordinator?.cancelPendingAuthorization()
        oauthPresentation.error = nil
    }

    @MainActor
    func cancelNativeOAuthMFAChallenge() {
        if mfaVM?.origin == .brokerOAuth {
            nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
        }
        showingMFAChallenge = false
        mfaVM = nil
    }

    @MainActor
    func dismissNativeOAuthError() {
        appleSignInError = nil
        passkeySignInError = nil
        oauthPresentation.error = nil
        if oauthPresentation.isPresentingExpiration {
            nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
            oauthPresentation.isPresentingExpiration = false
        }
    }
}
