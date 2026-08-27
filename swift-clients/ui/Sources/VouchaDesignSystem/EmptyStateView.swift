import SwiftUI
import VouchaLocalization

public struct EmptyStateView: View {
    @Environment(\.locale)
    private var locale
    public let icon: String
    public let title: UiVerbatimText
    public let message: UiVerbatimText?
    public let actionTitle: UiVerbatimText?
    public let action: (() -> Void)?

    public init(
        icon: String,
        title: UiVerbatimText,
        message: UiVerbatimText? = nil,
        actionTitle: UiVerbatimText? = nil,
        action: (() -> Void)? = nil
    ) {
        self.icon = icon
        self.title = title
        self.message = message
        self.actionTitle = actionTitle
        self.action = action
    }

    public var body: some View {
        VStack(spacing: Spacing.md) {
            Image(systemName: icon)
                .font(.system(size: 48))
                .foregroundStyle(Colors.secondaryLabel)

            Text(UiMessages.string(title, locale: locale))
                .font(Typography.headline)

            if let message {
                Text(UiMessages.string(message, locale: locale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .multilineTextAlignment(.center)
            }

            if let actionTitle, let action {
                Button(UiMessages.string(actionTitle, locale: locale), action: action)
                    .buttonStyle(.borderedProminent)
                    .padding(.top, Spacing.sm)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(Spacing.xl)
    }
}
