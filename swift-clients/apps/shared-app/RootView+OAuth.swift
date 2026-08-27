import VouchaCore

extension RootView {
    func restoreSessionAndNativeAuthorizationsOnLaunch() async {
        guard viewModelFactory.restoresSessionOnLaunch else { return }
        await viewModelFactory.sessionManager.restoreSession()
        await viewModelFactory.nativeOAuthAuthorizationCoordinator.resumePendingAuthorization()
        await resumePendingNativeBlueskyLink(factory: viewModelFactory)
    }

    var currentNativeOAuthMFAAttemptId: String? {
        viewModelFactory.nativeOAuthAuthorizationCoordinator.result?.mfaLoginAttemptId
    }

    func handleNativeOAuthAuthorizationResult(_ result: NativeOAuthAuthorizationResult?) {
        guard let result else { return }
        switch result {
        case .authenticated:
            viewModelFactory.nativeOAuthAuthorizationCoordinator.acknowledgeResult()
            completeSignIn()
        case .mfaRequired:
            showingSignIn = true
        case .connected:
            routeNativeTargetPath("/my/identity")
        case .expired(_, .authenticate):
            showingSignIn = true
        case .expired(_, .connect):
            routeNativeTargetPath("/my/identity")
        }
    }
}
