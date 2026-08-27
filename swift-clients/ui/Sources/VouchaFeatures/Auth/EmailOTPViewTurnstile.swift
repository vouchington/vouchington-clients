import SwiftUI
import VouchaCore

extension View {
    func emailOTPTurnstileSheet(
        siteKey: String?,
        isPresented: Binding<Bool>,
        viewModel: EmailOTPViewModel
    ) -> some View {
        sheet(isPresented: isPresented) {
            NativeTurnstileChallengeView(siteKey: siteKey ?? AppConfig.shared.requiredTurnstileSiteKey) { token in
                viewModel.turnstileToken = token
                isPresented.wrappedValue = false
            }
        }
    }
}
