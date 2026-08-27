import SwiftUI
import VouchaCore
import VouchaModels

public struct SettingsSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: SettingsViewModel
    @State
    var notificationSettingsViewModel: NotificationSettingsViewModel
    @State
    var notificationA11yActivator: NotificationA11yActivator
    @AccessibilityFocusState
    var isNotificationSettingsHeadingFocused: Bool
    @State
    var pendingSessionRevocation: AuthSession?
    @State
    var confirmingAllSessionRevocation = false
    @Environment(\.scenePhase)
    var scenePhase
    @Environment(\.openURL)
    var openURL
    @State
    var confirmingBlueskyDisconnect = false
    @State
    var confirmingOAuthDisconnect: NativeOAuthProvider?
    let imageBaseURL: URL
    let onNavigateToTargetPath: (String) -> Void
    let fediverseEnabled: Bool
    let focusedSection: SettingsSurfaceFocusedSection?
    let notificationActivationToken: Int

    public init(
        viewModel: SettingsViewModel,
        imageBaseURL: URL = AppConfig.shared.imageBaseURL,
        fediverseEnabled: Bool = false,
        focusedSection: SettingsSurfaceFocusedSection? = nil,
        notificationSettingsViewModel: NotificationSettingsViewModel? = nil,
        a11yActivator: NotificationA11yActivator = .init(),
        notificationActivationToken: Int = 0,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        self.viewModel = viewModel
        _notificationSettingsViewModel = State(
            initialValue: notificationSettingsViewModel ?? NotificationSettingsViewModel(client: viewModel.client)
        )
        _notificationA11yActivator = State(initialValue: a11yActivator)
        self.imageBaseURL = imageBaseURL
        self.fediverseEnabled = fediverseEnabled
        self.focusedSection = focusedSection
        self.notificationActivationToken = notificationActivationToken
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

}

public enum SettingsSurfaceFocusedSection: Equatable, Sendable {
    case notifications
}
