import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ProfileView {
    var signOutSection: some View {
        Button(role: .destructive) {
            Task { await viewModel.signOut() }
        } label: {
            Text(UiMessages.string(.nativeSwiftProfileSignOut, locale: nativeUiLocale))
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(.borderedProminent)
        .tint(.red)
        .padding(.top, Spacing.md)
    }
}
