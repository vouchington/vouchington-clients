import SwiftUI
import VouchaCore

extension View {
    func emailOTPTurnstileSheet(
        siteKey: String?,
        isPresented: Binding<Bool>,
        viewModel: EmailOTPViewModel
    ) -> some View {
        sheet(isPresented: isPresented) {
            emailOTPTurnstileChallenge(siteKey: siteKey, isPresented: isPresented, viewModel: viewModel)
        }
    }
}

@MainActor
func emailOTPTurnstileChallenge(
    siteKey: String?,
    isPresented: Binding<Bool>,
    viewModel: EmailOTPViewModel
) -> NativeTurnstileChallengeView {
    NativeTurnstileChallengeView(
        siteKey: siteKey ?? AppConfig.shared.requiredTurnstileSiteKey,
        action: .authEmailAddressTokens,
        baseURL: viewModel.turnstileBaseURL
    ) { token in
        viewModel.turnstileToken = token
        isPresented.wrappedValue = false
    }
}
