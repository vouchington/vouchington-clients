import SwiftUI
import VouchaLocalization

extension CommunityAgentAndAutomodControls {
    func actionButton(
        _ title: UiMessageKey,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage) {
            guard isSignedIn else {
                showSignIn()
                return
            }
            action()
        }
    }
}
