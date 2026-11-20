import SwiftUI
import VouchaLocalization
import VouchaModels

/// Renders a single notification row: unread indicator dot, title, optional body, and relative timestamp.
public struct NotificationCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    public let notification: VouchaNotification
    public let title: UiVerbatimText
    /// Nil for a title-only row.
    public let detail: UiVerbatimText?
    /// Overrides `notification.readAt`; pass `true` to suppress the unread indicator after a local optimistic read.
    public let isRead: Bool

    public init(
        notification: VouchaNotification,
        title: UiVerbatimText,
        detail: UiVerbatimText? = nil,
        isRead: Bool = false
    ) {
        self.notification = notification
        self.title = title
        self.detail = detail
        self.isRead = isRead
    }

    private var isUnread: Bool {
        !isRead && notification.readAt == nil
    }

    public var body: some View {
        HStack(alignment: .top, spacing: Spacing.sm) {
            Circle()
                .fill(isUnread ? Colors.primary : Color.clear)
                .frame(width: 8, height: 8)
                .padding(.top, 6)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(title, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .fontWeight(isUnread ? .semibold : .regular)
                    .foregroundStyle(.primary)
                    .lineLimit(2)
                if let detail {
                    Text(UiMessages.string(detail, locale: nativeUiLocale))
                        .font(Typography.body)
                        .foregroundStyle(.secondary)
                        .lineLimit(3)
                }
                Text(notification.createdAt, style: .relative)
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .padding(.vertical, Spacing.xs)
    }
}
