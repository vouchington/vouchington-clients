import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ProfileView {
    var signOutSection: some View {
        VStack(spacing: Spacing.sm) {
            Button(role: .destructive) {
                Task { await viewModel.signOut() }
            } label: {
                Text(UiMessages.string(.nativeSwiftProfileSignOut, locale: nativeUiLocale))
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .tint(.red)
            if let error = viewModel.signOutErrorMessage {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .foregroundStyle(.red)
            }
        }
        .padding(.top, Spacing.md)
    }
}
