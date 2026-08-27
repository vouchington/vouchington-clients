import SwiftUI

extension EmailOTPView {
    func mfaChallengeStep(loginAttemptId: String) -> some View {
        let challengeVM = viewModel.mfaChallengeVM
            ?? MFAChallengeViewModel(loginAttemptId: loginAttemptId, signInService: signInService)
        return MFAChallengeView(viewModel: challengeVM) {
            viewModel.didCompleteMFA()
        }
    }
}
