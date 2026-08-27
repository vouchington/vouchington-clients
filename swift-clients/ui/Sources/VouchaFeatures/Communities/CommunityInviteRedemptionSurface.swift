import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityInviteRedemptionSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: CommunityInviteRedemptionViewModel
    var isSignedIn = true
    var showSignIn: () -> Void = {}

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            EmptyStateView(
                icon: "envelope.open",
                title: .message(.nativeSwiftEmptyStateCommunityInvite),
                message: .message(
                    .nativeSwiftEmptyStateCommunityInviteMessage,
                    parameters: ["code": viewModel.code]
                )
            )
            Button(UiMessages.string(.nativeSwiftCommunitiesRedeemInvite, locale: nativeUiLocale)) {
                guard isSignedIn else {
                    showSignIn()
                    return
                }
                Task { await viewModel.redeem() }
            }
            .buttonStyle(.borderedProminent)
            stateText(viewModel.state, locale: nativeUiLocale)
            if let status = viewModel.statusMessage {
                Text(UiMessages.string(status, locale: nativeUiLocale)).font(Typography.caption)
            }
        }
    }
}
