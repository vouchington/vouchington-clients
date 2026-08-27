import SwiftUI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

extension SettingsSurface {
    var oauthAccountsSection: some View {
        section(.nativeSwiftSettingsAccount, systemImage: "person.crop.circle.badge.checkmark") {
            ForEach(NativeOAuthProvider.allCases) { provider in
                oauthAccountRow(provider)
            }
            if let error = viewModel.oauthErrorMessage {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale))
                    .foregroundStyle(.red)
            }
            oauthAuthorizationRecoveryControls
        }
    }

    @ViewBuilder
    var oauthAuthorizationRecoveryControls: some View {
        if viewModel.canRetryOAuthResultConsumption {
            HStack {
                Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                    Task { await viewModel.retryOAuthResultConsumption() }
                }
                Button(
                    UiMessages.string(.commonCancel, locale: nativeUiLocale),
                    role: .cancel
                ) {
                    viewModel.cancelOAuthResultConsumption()
                }
            }
        } else if viewModel.canRecoverOAuthFinalization {
            HStack {
                Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                    Task { await viewModel.retryOAuthFinalization() }
                }
                Button(
                    UiMessages.string(.commonCancel, locale: nativeUiLocale),
                    role: .cancel
                ) {
                    viewModel.cancelOAuthAuthorization()
                }
            }
        } else if viewModel.canDismissOAuthExpiration {
            Button(UiMessages.string(.nativeCommonOk, locale: nativeUiLocale)) {
                viewModel.dismissOAuthExpiration()
            }
        } else if viewModel.canRetryOAuthCapabilities {
            Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                Task { await viewModel.retryOAuthCapabilityLoading() }
            }
        } else if viewModel.canCancelOAuthAuthorization {
            Button(
                UiMessages.string(.commonCancel, locale: nativeUiLocale),
                role: .cancel
            ) {
                viewModel.cancelOAuthAuthorization()
            }
        }
    }

    private func oauthAccountRow(_ provider: NativeOAuthProvider) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(provider.titleKey, locale: nativeUiLocale))
                .font(Typography.headline)
            if let account = viewModel.account(for: provider) {
                if let name = account.name, !name.isEmpty {
                    Text(verbatim: UiMessages.string(.externalProvider(name), locale: nativeUiLocale))
                }
                if let emailAddress = account.emailAddress, !emailAddress.isEmpty {
                    Text(verbatim: UiMessages.string(.externalProvider(emailAddress), locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(Colors.secondaryLabel)
                }
                Button(UiMessages.string(.nativeSwiftSettingsBlueskyDisconnect, locale: nativeUiLocale)) {
                    confirmingOAuthDisconnect = provider
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.oauthProviderInFlight == provider)
            } else if viewModel.nativeOAuthAuthorizationCoordinator?.supports(
                provider: provider,
                purpose: .connect
            ) == true {
                Button(UiMessages.string(.nativeSwiftSettingsBlueskyConnect, locale: nativeUiLocale)) {
                    Task {
                        await viewModel.beginOAuthConnection(provider: provider) { url in
                            openURL(url)
                        }
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(!viewModel.canConnectOAuthAccount(provider: provider))
            }
        }
    }
}
