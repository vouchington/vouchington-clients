import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

@ViewBuilder
func stateText(_ state: CommunitySurfaceState, locale: Locale = Locale(identifier: "en")) -> some View {
    switch state {
    case .requiredTurnstile:
        Text(UiMessages.string(.nativeSwiftCommunitiesVerificationRequired, locale: locale)).font(Typography.caption)
    case let .error(message):
        Text(UiMessages.string(message, locale: locale)).font(Typography.caption).foregroundStyle(.red)
    default:
        EmptyView()
    }
}
