import SwiftUI
import VouchaDesignSystem

extension SettingsSurface {
    public var body: some View {
        settingsConfirmationDialogs(
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.xl) {
                    if focusedSection == .notifications {
                        notificationSection
                    } else if viewModel.identity == nil, case .loading = viewModel.state {
                        LoadingView()
                    } else if case let .error(error) = viewModel.state, viewModel.identity == nil {
                        ErrorStateView(error: error) { await viewModel.reload() }
                    } else {
                        statusBanner
                        allSettingsSections
                    }
                }
                .padding(Spacing.md)
            }
            .task { await viewModel.load() }
            .task {
                await viewModel.loadOAuthCapabilities()
                viewModel.synchronizeOAuthError()
                await viewModel.consumeOAuthAuthorizationResult(
                    viewModel.nativeOAuthAuthorizationCoordinator?.result
                )
            }
            .task(id: pollingTaskID) {
                guard scenePhase == .active else { return }
                await viewModel.pollDataRequestIfNeeded()
            }
            .task(id: viewModel.blueskyExpiryTaskID) { await viewModel.waitForBlueskyExpiry() }
            .onChange(of: scenePhase) { _, phase in
                if phase == .active {
                    viewModel.reconcileBlueskyLinkState()
                }
            }
            .onChange(of: viewModel.nativeOAuthAuthorizationCoordinator?.result) { _, result in
                Task { await viewModel.consumeOAuthAuthorizationResult(result) }
            }
            .onChange(of: viewModel.nativeOAuthAuthorizationCoordinator?.errorMessage) { _, _ in
                viewModel.synchronizeOAuthError()
            }
            .onChange(of: viewModel.nativeOAuthAuthorizationCoordinator?.capabilityErrorMessage) { _, _ in
                viewModel.synchronizeOAuthError()
            }
            .refreshable {
                await reload()
            }
        )
    }

    func reload() async {
        async let settingsReload: Void = viewModel.reload()
        if focusedSection == .notifications {
            async let notificationsReload: Void = notificationSettingsViewModel.load()
            await settingsReload
            await notificationsReload
        } else {
            await settingsReload
        }
    }

    private var allSettingsSections: some View {
        Group {
            identitySection
            oauthAccountsSection
            if fediverseEnabled {
                blueskySection
            }
            emailAddressesSection
            profileSection
            privacySection
            notificationSection
            legalSupportSection
            membershipSection
            sessionsSection
            apiKeysSection
            connectedAppsSection
            localLLMSection
            pushSubscriptionsSection
            dataSection
        }
    }

    private var pollingTaskID: String? {
        scenePhase == .active ? viewModel.dataRequestPollingTaskID : nil
    }
}
