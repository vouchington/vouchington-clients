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
            .disabled(viewModel.isLoading)
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
                        }
                        Spacer(minLength: 0)
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

    var pushSubscriptionsSection: some View {
        section(.nativeSwiftSettingsPushSubscriptions, systemImage: "bell") {
            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(viewModel.pushSubscriptions, id: \.id) { subscription in
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(subscription.endpoint)
                                .lineLimit(1)
                            Text(subscription.userAgent)
                                .font(Typography.caption)
                                .foregroundStyle(Colors.secondaryLabel)
                        }
                        Spacer(minLength: 0)
                        Button(
                            UiMessages.string(.nativeSwiftSettingsRevoke, locale: nativeUiLocale),
                            role: .destructive
                        ) {
                            Task { await viewModel.revokePushSubscription(id: subscription.id) }
                        }
                        .buttonStyle(.bordered)
                    }
                }
                HybridPaginationControl(
                    hasMore: viewModel.pushSubscriptionPagination.hasMore,
                    isLoading: viewModel.pushSubscriptionPagination.isLoading,
                    hasError: viewModel.pushSubscriptionPagination.lastError != nil,
                    accessibilityIdentifier: "push-subscriptions-pagination"
                ) {
                    await viewModel.loadMorePushSubscriptions()
                }
            }
        }
    }

    var dataSection: some View {
        section(.nativeSwiftSettingsYourData, systemImage: "doc.richtext") {
            if let dataRequest = viewModel.dataRequest {
                Text(UiMessages.string(
                    .nativeSwiftSettingsExportStatus,
                    parameters: [
                        "status": UiMessages.string(
                            dataRequest.status.titleKey,
                            locale: nativeUiLocale
                        )
                    ],
                    locale: nativeUiLocale
                ))
                .font(Typography.subheadline)
                if let downloadURL = viewModel.dataRequestDownloadURL {
                    Link(
                        UiMessages.string(.nativeSwiftSettingsDownloadDataExport, locale: nativeUiLocale),
                        destination: downloadURL
                    )
                    .font(Typography.subheadline)
                }
            } else {
                Text(UiMessages.string(.nativeSwiftSettingsNoExportRequested, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
            }

            Button(UiMessages.string(.nativeSwiftSettingsRequestDataExport, locale: nativeUiLocale)) {
                Task { await viewModel.requestDataExport() }
            }
            .buttonStyle(.bordered)

            TextField(
                UiMessages.string(.nativeSwiftSettingsTypeDeleteMyAccountToConfirm, locale: nativeUiLocale),
                text: $viewModel.deleteConfirmation
            )
            .textFieldStyle(.roundedBorder)
            Button(UiMessages.string(.nativeSwiftSettingsDeleteAccount, locale: nativeUiLocale), role: .destructive) {
                Task { await viewModel.deleteAccount() }
            }
            .buttonStyle(.borderedProminent)
            .disabled(viewModel.deleteConfirmation.trimmingCharacters(in: .whitespacesAndNewlines)
                .lowercased() != "delete my account")
        }
    }

    func section(
        _ title: UiMessageKey,
        systemImage: String,
        accessibilityIdentifier: String? = nil,
        isAccessibilityFocusTarget: Bool = false,
        @ViewBuilder content: () -> some View
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if isAccessibilityFocusTarget {
                Label(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage)
                    .font(Typography.headline)
                    .accessibilityAddTraits(.isHeader)
                    .accessibilityIdentifier(accessibilityIdentifier ?? "")
                    .accessibilityFocused($isNotificationSettingsHeadingFocused)
            } else {
                Label(UiMessages.string(title, locale: nativeUiLocale), systemImage: systemImage)
                    .font(Typography.headline)
                    .accessibilityAddTraits(accessibilityIdentifier == nil ? [] : .isHeader)
                    .accessibilityIdentifier(accessibilityIdentifier ?? "")
            }
            content()
            Divider()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    func privacyPicker(_ title: UiMessageKey, selection: Binding<UserPrivacyAudience>) -> some View {
        Picker(UiMessages.string(title, locale: nativeUiLocale), selection: selection) {
            ForEach(UserPrivacyAudience.allCases, id: \.self) { audience in
                Text(UiMessages.string(audience.titleKey, locale: nativeUiLocale))
                    .tag(audience)
            }
        }
        .pickerStyle(.menu)
    }

    var editorBorder: some View {
        RoundedRectangle(cornerRadius: 8, style: .continuous)
            .strokeBorder(.quaternary, lineWidth: 1)
    }
}
