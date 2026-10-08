import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityAutomodActionStatusView: View {
    @Environment(\.locale)
    var nativeUiLocale
    let action: CommunityAutomodActionSetting?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeSwiftCommunitiesAutomod, locale: nativeUiLocale))
                .font(Typography.subheadline)
            if let action {
                Text(verbatim: UiMessages.string(.protocolValue(action.rawValue), locale: nativeUiLocale))
                    .accessibilityIdentifier("community-automod-action")
            }
        }
    }
}
