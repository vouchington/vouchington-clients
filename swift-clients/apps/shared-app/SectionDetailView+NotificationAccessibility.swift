import VouchaFeatures

extension SectionDetailView {
    var notificationA11yActivator: NotificationA11yActivator? {
        #if DEBUG
            NotificationSettingsUITestingHooks.a11yActivatorIfEnabled()
        #else
            nil
        #endif
    }
}
