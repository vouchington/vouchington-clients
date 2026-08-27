import SwiftUI
import VouchaLocalization

public struct RelationActionButtons: View {
    @Environment(\.locale)
    private var nativeUiLocale
    public let isSaved: Bool
    public let isHidden: Bool
    public let onToggleSaved: (() -> Void)?
    public let onToggleHidden: (() -> Void)?

    public init(
        isSaved: Bool,
        isHidden: Bool,
        onToggleSaved: (() -> Void)? = nil,
        onToggleHidden: (() -> Void)? = nil
    ) {
        self.isSaved = isSaved
        self.isHidden = isHidden
        self.onToggleSaved = onToggleSaved
        self.onToggleHidden = onToggleHidden
    }

    public var body: some View {
        if onToggleSaved != nil || onToggleHidden != nil {
            HStack(spacing: Spacing.xs) {
                relationButton(
                    title: isSaved ? .nativeSwiftDesignSystemSaved : .nativeSwiftCommonSave,
                    systemImage: isSaved ? "bookmark.fill" : "bookmark",
                    action: onToggleSaved
                )
                relationButton(
                    title: isHidden ? .nativeSwiftDesignSystemHidden : .nativeSwiftDesignSystemHide,
                    systemImage: isHidden ? "eye.slash.fill" : "eye.slash",
                    action: onToggleHidden
                )
            }
        }
    }

    @ViewBuilder
    private func relationButton(title: UiMessageKey, systemImage: String, action: (() -> Void)?) -> some View {
        if let action {
            Button(action: action) {
                Label(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage)
                    .labelStyle(.iconOnly)
            }
            .buttonStyle(.borderless)
            .accessibilityLabel(UiMessages.string(title, locale: nativeUiLocale))
        }
    }
}
