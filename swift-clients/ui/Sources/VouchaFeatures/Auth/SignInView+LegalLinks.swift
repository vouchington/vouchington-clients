import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SignInView {
    func navigateToLegalTargetPath(_ targetPath: String) {
        onNavigateToTargetPath(targetPath)
        dismiss()
    }

    var legalLinksSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(.nativeLegalTitle, locale: locale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)

            Button {
                navigateToLegalTargetPath("/article/privacy-policy")
            } label: {
                Label(UiMessages.string(.nativeLegalPrivacyPolicy, locale: locale), systemImage: "lock")
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)

            Button {
                navigateToLegalTargetPath("/article/terms-of-service")
            } label: {
                Label(UiMessages.string(.nativeLegalTermsOfService, locale: locale), systemImage: "doc.text")
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)

            Button {
                navigateToLegalTargetPath("/article/community-guidelines")
            } label: {
                Label(
                    UiMessages.string(.nativeLegalCommunityGuidelines, locale: locale),
                    systemImage: "person.3"
                )
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .buttonStyle(.plain)
        }
        .padding(.horizontal, Spacing.xl)
    }
}
