import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct NativeUserProfileReferralLinkRow: View {
    let link: NativeReferralLink
    let onOpen: () -> Void

    var title: String {
        link.label?.ifNotEmpty ?? link.referralProgramName?.ifNotEmpty ?? link.url ?? link.id
    }

    var body: some View {
        Button(action: onOpen) {
            HStack(spacing: Spacing.sm) {
                Image(systemName: "link")
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(title).font(Typography.subheadline).fontWeight(.semibold)
                    if let url = link.url {
                        Text(url)
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: 0)
            }
            .padding(.vertical, Spacing.xs)
        }
        .buttonStyle(.plain)
        .accessibilityIdentifier("public-profile-referral-link-row")
    }
}
