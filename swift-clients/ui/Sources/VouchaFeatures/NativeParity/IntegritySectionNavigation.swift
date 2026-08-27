import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct IntegritySectionNavigation: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let domain: IntegrityDomain
    let selectedPenalties: Bool
    let onNavigate: (String) -> Void

    var body: some View {
        HStack(spacing: Spacing.sm) {
            Button(text(.nativeSwiftIntegrityFlags)) { onNavigate(domain.flagsPath) }
                .disabled(!selectedPenalties)
            Button(text(.nativeSwiftIntegrityPenalties)) { onNavigate(domain.penaltiesPath) }
                .disabled(selectedPenalties)
        }
    }

    private func text(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}
