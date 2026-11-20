import VouchaAPI
import VouchaModels

extension NotificationsListViewModel {
    /// The localized title and optional body for a loaded notification row.
    public func content(for notification: VouchaNotification) -> NotificationContent {
        NotificationTextRenderer.render(
            notification,
            hydration: hydration(for: notification),
            webOrigin: client.baseURL
        )
    }

    /// The facts delivered with the page that carried `notification`; empty once reset.
    func hydration(for notification: VouchaNotification) -> NotificationHydration {
        hydrationByNotificationId[notification.id] ?? NotificationHydration()
    }
}
