import SwiftUI
import VouchaModels

/// Renders a single notification row: unread indicator dot, title, body, and relative timestamp.
public struct NotificationCard: View {
    public let notification: VouchaNotification
    /// Overrides `notification.readAt`; pass `true` to suppress the unread indicator after a local optimistic read.
    public let isRead: Bool

    public init(notification: VouchaNotification, isRead: Bool = false) {
        self.notification = notification
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
                Text(notification.title)
                    .font(Typography.subheadline)
                    .fontWeight(isUnread ? .semibold : .regular)
                    .foregroundStyle(.primary)
                    .lineLimit(2)
                Text(notification.body)
                    .font(Typography.body)
                    .foregroundStyle(.secondary)
                    .lineLimit(3)
                Text(notification.createdAt, style: .relative)
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .padding(.vertical, Spacing.xs)
    }
}
