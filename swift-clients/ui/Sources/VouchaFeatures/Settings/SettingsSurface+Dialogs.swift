import SwiftUI
import VouchaLocalization
import VouchaModels

extension SettingsSurface {
    func settingsConfirmationDialogs(_ content: some View) -> some View {
        oauthDisconnectDialog(blueskyDisconnectDialog(allSessionsDialog(sessionRevocationDialog(content))))
    }

    private func sessionRevocationDialog(_ content: some View) -> some View {
        content
            .confirmationDialog(
                UiMessages.string(
                    pendingSessionRevocation?.isCurrent == true
                        ? .nativeSwiftSettingsSignOutCurrentConfirmation
                        : .nativeSwiftSettingsSignOutSessionConfirmation,
                    locale: nativeUiLocale
                ),
                isPresented: Binding(
                    get: { pendingSessionRevocation != nil },
                    set: {
                        if !$0 {
                            pendingSessionRevocation = nil
                        }
                    }
                ),
                titleVisibility: .visible
            ) {
                Button(UiMessages.string(.nativeSwiftProfileSignOut, locale: nativeUiLocale), role: .destructive) {
                    if let session = pendingSessionRevocation {
                        Task { await viewModel.revokeSession(id: session.id) }
                    }
                    pendingSessionRevocation = nil
                }
            } message: {
                Text(UiMessages.string(
                    pendingSessionRevocation?.isCurrent == true
                        ? .nativeSwiftSettingsSignOutCurrentMessage
                        : .nativeSwiftSettingsSignOutSessionMessage,
                    locale: nativeUiLocale
                ))
            }
    }

    private func allSessionsDialog(_ content: some View) -> some View {
        content.confirmationDialog(
            UiMessages.string(.nativeSwiftSettingsSignOutAllDevicesConfirmationTitle, locale: nativeUiLocale),
            isPresented: $confirmingAllSessionRevocation,
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftSettingsSignOutAllDevices, locale: nativeUiLocale),
                role: .destructive
            ) {
                Task { await viewModel.revokeAllSessions() }
            }
        } message: {
            Text(UiMessages.string(.nativeSwiftSettingsThisWillSignOutVouchaOnEveryDevice, locale: nativeUiLocale))
        }
    }

    private func blueskyDisconnectDialog(_ content: some View) -> some View {
        content.confirmationDialog(
            UiMessages.string(.nativeSwiftSettingsBlueskyDisconnectConfirmation, locale: nativeUiLocale),
            isPresented: $confirmingBlueskyDisconnect,
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftSettingsBlueskyDisconnect, locale: nativeUiLocale),
                role: .destructive
            ) {
                Task { await viewModel.disconnectBluesky() }
            }
        }
    }

    private func oauthDisconnectDialog(_ content: some View) -> some View {
        content.confirmationDialog(
            confirmingOAuthDisconnect.map {
                UiMessages.string($0.titleKey, locale: nativeUiLocale)
            } ?? UiMessages.string(.nativeSwiftSettingsAccount, locale: nativeUiLocale),
            isPresented: Binding(
                get: { confirmingOAuthDisconnect != nil },
                set: {
                    if !$0 {
                        confirmingOAuthDisconnect = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftSettingsBlueskyDisconnect, locale: nativeUiLocale),
                role: .destructive
            ) {
                if let provider = confirmingOAuthDisconnect {
                    Task { await viewModel.disconnectOAuthAccount(provider: provider) }
                }
                confirmingOAuthDisconnect = nil
            }
        }
    }
}
