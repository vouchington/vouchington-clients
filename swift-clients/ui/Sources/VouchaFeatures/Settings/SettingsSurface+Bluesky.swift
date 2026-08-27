import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    var blueskySection: some View {
        section(.nativeSwiftSettingsBluesky, systemImage: "cloud") {
            if let account = viewModel.identity?.blueskyAccount {
                Text(verbatim: account.handle ?? account.did)
                    .font(Typography.headline)
                Text(verbatim: account.did)
                    .font(Typography.caption.monospaced())
                    .textSelection(.enabled)
                Button(UiMessages.string(
                    viewModel.blueskyLinkState == .disconnecting
                        ? .nativeSwiftSettingsBlueskyDisconnecting
                        : .nativeSwiftSettingsBlueskyDisconnect,
                    locale: nativeUiLocale
                )) {
                    confirmingBlueskyDisconnect = true
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.blueskyLinkState == .disconnecting)
            } else {
                TextField(
                    UiMessages.string(.nativeSwiftSettingsBlueskyHandle, locale: nativeUiLocale),
                    text: $viewModel.blueskyHandle,
                    prompt: Text(UiMessages.string(
                        .nativeSwiftSettingsBlueskyHandlePlaceholder,
                        locale: nativeUiLocale
                    ))
                )
                .textFieldStyle(.roundedBorder)
                if viewModel.blueskyLinkState == .awaitingCallback {
                    Button(UiMessages.string(.nativeSwiftSettingsBlueskyCancel, locale: nativeUiLocale)) {
                        viewModel.cancelBlueskyLink()
                    }
                    .buttonStyle(.bordered)
                } else {
                    Button(UiMessages.string(blueskyConnectButtonKey, locale: nativeUiLocale)) {
                        Task {
                            await viewModel.beginBlueskyLink { url in
                                openURL(url)
                            }
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(!viewModel.canBeginBlueskyLink)
                }
            }
            blueskyStatus
        }
    }

    @ViewBuilder private var blueskyStatus: some View {
        switch viewModel.blueskyLinkState {
        case let .error(message):
            Text(verbatim: UiMessages.string(message, locale: nativeUiLocale)).foregroundStyle(.red)
        case .cancelled:
            Text(UiMessages.string(.nativeSwiftSettingsBlueskyCancelled, locale: nativeUiLocale))
        case .expired:
            Text(UiMessages.string(.nativeSwiftSettingsBlueskyExpired, locale: nativeUiLocale))
        case .finalizing:
            Text(UiMessages.string(.nativeSwiftSettingsBlueskyFinalizing, locale: nativeUiLocale))
        case .success:
            Text(UiMessages.string(.nativeSwiftSettingsBlueskyConnected, locale: nativeUiLocale))
        case .idle, .linking, .awaitingCallback, .disconnecting:
            EmptyView()
        }
    }

    private var blueskyConnectButtonKey: UiMessageKey {
        switch viewModel.blueskyLinkState {
        case .linking: .nativeSwiftSettingsBlueskyConnecting
        case .finalizing: .nativeSwiftSettingsBlueskyFinalizing
        default: .nativeSwiftSettingsBlueskyConnect
        }
    }
}
