import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityVacationControls: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: CommunityDetailViewModel
    let isSignedIn: Bool
    let showSignIn: () -> Void
    var digestPreferenceUpdateCompleted: () -> Void = {}

    @State
    private var endsAt = Date()

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftCommunitiesVacation, locale: nativeUiLocale)).font(Typography.headline)
            DatePicker(UiMessages.string(.nativeSwiftCommunitiesEndsAt, locale: nativeUiLocale), selection: $endsAt)
            HStack {
                Button(
                    UiMessages.string(.nativeSwiftCommunitiesSetVacation, locale: nativeUiLocale),
                    systemImage: "moon.zzz"
                ) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.setModeratorVacation(endsAt: endsAt) }
                }
                Button(
                    UiMessages.string(.nativeSwiftCommunitiesClearVacation, locale: nativeUiLocale),
                    systemImage: "xmark.circle"
                ) {
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task { await viewModel.clearModeratorVacation() }
                }
            }
            digestSuppressionToggle
        }
    }

    private var digestSuppressionToggle: some View {
        Toggle(
            UiMessages.string(.nativeSwiftCommunitiesPauseCommunityDigestsWhileOnVacation, locale: nativeUiLocale),
            isOn: Binding(
                get: { viewModel.suppressCommunityDigestsWhileOnVacation },
                set: { suppress in
                    guard isSignedIn else {
                        showSignIn()
                        return
                    }
                    Task {
                        await viewModel.setSuppressCommunityDigestsWhileOnVacation(suppress)
                        digestPreferenceUpdateCompleted()
                    }
                }
            )
        )
    }
}
