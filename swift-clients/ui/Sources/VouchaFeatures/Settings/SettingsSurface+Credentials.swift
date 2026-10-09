import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension SettingsSurface {
    var apiKeyScopePicker: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeCredentialsChooseScopes, locale: nativeUiLocale))
            if case let .error(error) = viewModel.credentialState {
                ErrorStateView(error: error) { await viewModel.retryCredentialScopeCatalog() }
            } else if case .loading = viewModel.credentialState {
                ProgressView()
            }
            ForEach(viewModel.apiKeyScopes) { scope in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Toggle(isOn: Binding(
                        get: { viewModel.apiKeyScopeSelection.selectedScopes.contains(scope.scope) },
                        set: { viewModel.setApiKeyScope(scope.scope, selected: $0) }
                    )) {
                        Text(verbatim: UiMessages.string(.protocolValue(scope.scope), locale: nativeUiLocale))
                    }
                    .disabled(
                        !viewModel.apiKeyScopeSelection.canSelect(scope.scope) || viewModel.apiKeyCreationInFlight
                    )
                    .accessibilityIdentifier("api-key-scope-\(scope.scope)")
                    scopeMetadata(scope)
                    if let descriptionKey = scope.descriptionKey,
                       let titleKey = descriptionKey.titleKey {
                        Text(UiMessages.string(titleKey, locale: nativeUiLocale))
                    }
                    if let prerequisite = scope.requires {
                        Text(UiMessages.string(
                            .nativeCredentialsRequires,
                            parameters: ["scope": prerequisite], locale: nativeUiLocale
                        ))
                    }
                }
                .font(Typography.caption)
            }
        }
    }

    private func scopeMetadata(_ scope: CredentialScope) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            LabeledContent(UiMessages.string(.nativeCredentialsResource, locale: nativeUiLocale)) {
                Text(verbatim: UiMessages.string(.protocolValue(scope.resource), locale: nativeUiLocale))
            }
            LabeledContent(UiMessages.string(.nativeCredentialsAction, locale: nativeUiLocale)) {
                Text(UiMessages.string(scope.action.titleKey, locale: nativeUiLocale))
            }
            Text(UiMessages.string(scope.audience.titleKey, locale: nativeUiLocale))
        }
    }

    var connectedAppsSection: some View {
        section(.nativeCredentialsConnectedApps, systemImage: "app.connected.to.app.below.fill") {
            if case let .error(error) = viewModel.oauthGrantState {
                ErrorStateView(error: error) { await viewModel.retryOAuthGrants() }
            } else if case .loading = viewModel.oauthGrantState {
                ProgressView()
            }
            if viewModel.hasNoOAuthGrants {
                Text(UiMessages.string(.nativeCredentialsNoConnectedApps, locale: nativeUiLocale))
            }
            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(viewModel.oauthGrants) { grant in
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: Spacing.xs) {
                            Text(verbatim: UiMessages.string(
                                .verbatim(grant.client.clientName),
                                locale: nativeUiLocale
                            ))
                            Text(UiMessages.string(
                                grant.client.verified ? .nativeSwiftCommonVerified : .nativeSwiftRouteSurfaceUnverified,
                                locale: nativeUiLocale
                            ))
                            LabeledContent(UiMessages.string(.nativeCredentialsResource, locale: nativeUiLocale)) {
                                Text(verbatim: UiMessages.string(
                                    .protocolValue(grant.resource),
                                    locale: nativeUiLocale
                                ))
                            }
                            LabeledContent(UiMessages.string(.nativeCredentialsScopes, locale: nativeUiLocale)) {
                                Text(verbatim: UiMessages.string(
                                    .protocolValue(grant.scopes.joined(separator: ", ")), locale: nativeUiLocale
                                ))
                            }
                            Text(grantActivity(grant))
                        }
                        Spacer(minLength: 0)
                        OAuthGrantRevokeButton(
                            grant: grant,
                            locale: nativeUiLocale,
                            isDisabled: viewModel.isLoading,
                            interactionState: .init()
                        ) {
                            await viewModel.revokeOAuthGrant(id: grant.id)
                        }
                    }
                    .font(Typography.caption)
                }
                HybridPaginationControl(
                    hasMore: viewModel.oauthGrantPagination.hasMore,
                    isLoading: viewModel.oauthGrantPagination.isLoading,
                    hasError: viewModel.oauthGrantPagination.lastError != nil,
                    accessibilityIdentifier: "oauth-grants-pagination"
                ) {
                    await viewModel.loadMoreOAuthGrants()
                }
            }
        }
    }

    private func grantActivity(_ grant: OAuthGrant) -> String {
        let consentedAt = UiMessages.date(grant.consentedAt, locale: nativeUiLocale, timeZone: .current)
        if let lastUsedAt = grant.lastUsedAt {
            return UiMessages.string(.nativeCredentialsGrantActivity, parameters: [
                "consentedAt": consentedAt,
                "lastUsedAt": UiMessages.date(lastUsedAt, locale: nativeUiLocale, timeZone: .current)
            ], locale: nativeUiLocale)
        }
        return UiMessages.string(
            .nativeCredentialsUnusedGrantActivity,
            parameters: ["consentedAt": consentedAt],
            locale: nativeUiLocale
        )
    }
}

private extension ScopeAction {
    var titleKey: UiMessageKey {
        self == .read ? .nativeCredentialsReadAction : .nativeCredentialsWriteAction
    }
}

private extension ScopeAudience {
    var titleKey: UiMessageKey {
        switch self {
        case .user: .nativeCredentialsUserAudience
        case .api: .nativeCredentialsApiAudience
        case .admin: .nativeCredentialsInvalidSelection
        }
    }
}

private extension ScopeDescriptionKey {
    var titleKey: UiMessageKey? {
        switch self {
        case .mcpUserFullAccess: .nativeCredentialsMcpUserFullAccess
        case .mcpAdminFullAccess: .nativeCredentialsMcpAdminFullAccess
        case .financialProfileRead, .financialProfileWrite, .spendingRead, .spendingWrite: nil
        }
    }
}
