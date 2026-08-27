import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    var legalSupportSection: some View {
        section(.nativeSwiftSettingsLegalAndSupport, systemImage: "shield") {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                navigationButton(.nativeLegalPrivacyPolicy, systemImage: "lock", targetPath: "/article/privacy-policy")
                navigationButton(
                    .nativeLegalTermsOfService,
                    systemImage: "doc.text",
                    targetPath: "/article/terms-of-service"
                )
                navigationButton(
                    .nativeLegalCommunityGuidelines,
                    systemImage: "person.3",
                    targetPath: "/article/community-guidelines"
                )
                navigationButton(
                    .nativeSwiftSettingsSupport,
                    systemImage: "questionmark.circle",
                    targetPath: "/chat/support"
                )
            }
        }
    }

    private func navigationButton(_ title: UiMessageKey, systemImage: String, targetPath: String) -> some View {
        Button {
            onNavigateToTargetPath(targetPath)
        } label: {
            Label(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .buttonStyle(.plain)
    }
}
