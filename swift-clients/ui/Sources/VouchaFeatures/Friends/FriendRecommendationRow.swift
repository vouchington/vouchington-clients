import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct FriendRecommendationRow: View {
    @Environment(\.locale)
    private var locale
    let recommendation: FriendRecommendation
    let user: PublicUser?
    let avatarURL: String?
    let isMutating: Bool
    let onFollow: () -> Void
    let onDismiss: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let user {
                UserRow(user: user, avatarURL: avatarURL)
            } else if !recommendation.providerFriendName
                .trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                Text(verbatim: UiMessages.string(
                    .userContent(recommendation.providerFriendName),
                    locale: locale
                ))
                .font(Typography.headline)
            } else {
                HStack(spacing: Spacing.xs) {
                    Text(UiMessages.string(.nativeSwiftNavigationVoucha, locale: locale))
                    Text(UiMessages.string(.nativeSwiftPresentationValuesMember, locale: locale))
                }
                .font(Typography.headline)
            }
            Text(UiMessages.string(recommendation.provider.titleKey, locale: locale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            HStack {
                Button(UiMessages.string(.nativeSwiftDesignSystemFollow, locale: locale), action: onFollow)
                    .buttonStyle(.borderedProminent)
                Button(UiMessages.string(.nativeSwiftIntegrityDismiss, locale: locale), action: onDismiss)
                    .buttonStyle(.bordered)
            }
            .disabled(isMutating)
        }
        .padding(.vertical, Spacing.xs)
    }
}
