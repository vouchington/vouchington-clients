import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeTagLimitCta: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let onNavigate: (String) -> Void

    var body: some View {
        VStack(spacing: Spacing.sm) {
            Image(systemName: "lock")
                .font(.system(size: 32))
                .foregroundStyle(Colors.secondaryLabel)
            Text(text(.nativeSwiftTagManagementLimitReachedTitle))
                .font(Typography.headline)
                .multilineTextAlignment(.center)
            Text(text(.nativeSwiftTagManagementLimitReachedMessage))
                .font(Typography.subheadline)
                .foregroundStyle(Colors.secondaryLabel)
                .multilineTextAlignment(.center)
            Button(text(.nativeSwiftTagManagementViewPlans)) {
                onNavigate("/plans")
            }
            .buttonStyle(.borderedProminent)
        }
        .frame(maxWidth: .infinity)
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
        .accessibilityIdentifier("tag-limit-cta")
    }

    private func text(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}
