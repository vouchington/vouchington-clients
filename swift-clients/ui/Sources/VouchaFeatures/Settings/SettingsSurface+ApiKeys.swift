import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension SettingsSurface {
    var apiKeysSection: some View {
        section(.nativeSwiftSettingsApiKeys, systemImage: "key") {
            TextField(
                UiMessages.string(.nativeSwiftReferralLinksManagementLabel, locale: nativeUiLocale),
                text: $viewModel.apiKeyLabel
            )
            .textFieldStyle(.roundedBorder)
            Picker(
                UiMessages.string(.nativeSwiftPresentationType, locale: nativeUiLocale),
                selection: $viewModel.apiKeyType
            ) {
                Text(UiMessages.string(.nativeSwiftSettingsRss, locale: nativeUiLocale)).tag(ApiKeyType.rss)
                Text(UiMessages.string(.nativeSwiftSettingsMcp, locale: nativeUiLocale)).tag(ApiKeyType.mcp)
            }
            .pickerStyle(.segmented)
            .disabled(viewModel.apiKeyCreationInFlight)
            Picker(
                UiMessages.string(.nativeApiKeysLifetimeLabel, locale: nativeUiLocale),
                selection: Binding(
                    get: { viewModel.apiKeyLifetimeDays },
                    set: { viewModel.setApiKeyLifetimeDays($0) }
                )
            ) {
                Text(UiMessages.string(.nativeApiKeysLifetime30, locale: nativeUiLocale)).tag(Optional(30))
                Text(UiMessages.string(.nativeApiKeysLifetime90, locale: nativeUiLocale)).tag(Optional(90))
                if !viewModel.isApiKeyAdministrator {
                    Text(UiMessages.string(.nativeApiKeysLifetime365, locale: nativeUiLocale)).tag(Optional(365))
                    Text(UiMessages.string(.nativeApiKeysLifetimeNone, locale: nativeUiLocale)).tag(Int?.none)
                }
            }
            .accessibilityIdentifier("api-key-lifetime")
            .disabled(viewModel.apiKeyCreationInFlight)
            apiKeyScopePicker
            Button(UiMessages.string(.nativeSwiftSettingsCreateApiKey, locale: nativeUiLocale)) {
                Task { await viewModel.createApiKey() }
            }
            .buttonStyle(.borderedProminent)
            .disabled(!viewModel.canCreateApiKey)

            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(viewModel.apiKeys) { key in
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(key.label)
                            Text(key.prefix)
                                .font(Typography.caption.monospaced())
                                .foregroundStyle(Colors.secondaryLabel)
                            Text(UiMessages.string(viewModel.apiKeyStatus(key), locale: nativeUiLocale))
                                .font(Typography.caption)
                            if let expiry = key.expiresAt {
                                Text(UiMessages.string(
                                    .nativeApiKeysExpiresAt,
                                    parameters: ["date": UiMessages.date(
                                        expiry, date: .abbreviated, time: .omitted,
                                        locale: nativeUiLocale, timeZone: .current
                                    )],
                                    locale: nativeUiLocale
                                ))
                                .font(Typography.caption)
                            } else {
                                Text(UiMessages.string(.nativeApiKeysLifetimeNone, locale: nativeUiLocale))
                                    .font(Typography.caption)
                            }
                        }
                        Spacer(minLength: 0)
                        if viewModel.canRotateApiKey(key) {
                            Button(UiMessages.string(.nativeApiKeysRotate, locale: nativeUiLocale)) {
                                Task { await viewModel.rotateApiKey(id: key.id) }
                            }
                            .buttonStyle(.bordered)
                            .accessibilityIdentifier("rotate-api-key-\(key.id)")
                        }
                        Button(
                            UiMessages.string(.nativeSwiftSettingsRevoke, locale: nativeUiLocale),
                            role: .destructive
                        ) {
                            Task { await viewModel.revokeApiKey(id: key.id) }
                        }
                        .buttonStyle(.bordered)
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.apiKeyPagination.hasMore,
                    isLoading: viewModel.apiKeyPagination.isLoading,
                    hasError: viewModel.apiKeyPagination.lastError != nil,
                    accessibilityIdentifier: "api-keys-pagination"
                ) {
                    await viewModel.loadMoreApiKeys()
                }
            }
        }
    }

}
