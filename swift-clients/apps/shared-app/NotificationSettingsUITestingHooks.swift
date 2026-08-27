import Foundation
import VouchaFeatures

#if DEBUG
    enum NotificationSettingsUITestingHooks {
        static let activated = Notification.Name("notification-settings-ui-testing-activated")

        static func a11yActivatorIfEnabled() -> NotificationA11yActivator? {
            guard ProcessInfo.processInfo.arguments.contains(
                AppLaunchContent.notificationSettingsUITestingArgument
            ) else { return nil }
            return fixtureA11yActivator
        }

        private static let fixtureA11yActivator = NotificationA11yActivator(activationObserver: { message in
            NotificationCenter.default.post(name: activated, object: message)
        })
    }
#endif
