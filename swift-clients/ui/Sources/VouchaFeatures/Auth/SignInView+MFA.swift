import SwiftUI

extension SignInView {
    var nativeOAuthMFASheetIsPresented: Binding<Bool> {
        Binding(
            get: { showingMFAChallenge },
            set: { isPresented in
                if isPresented {
                    showingMFAChallenge = true
                } else if showingMFAChallenge {
                    cancelNativeOAuthMFAChallenge()
                }
            }
        )
    }

    @ViewBuilder
    var mfaSheet: some View {
        if let challengeVM = mfaVM {
            MFAChallengeView(
                viewModel: challengeVM,
                onSuccess: {
                    if challengeVM.origin == .brokerOAuth {
                        nativeOAuthAuthorizationCoordinator?.acknowledgeResult()
                    }
                    showingMFAChallenge = false
                    completeSuccessfulSignIn()
                    dismiss()
                },
                onCancel: cancelNativeOAuthMFAChallenge
            )
        }
    }
}
