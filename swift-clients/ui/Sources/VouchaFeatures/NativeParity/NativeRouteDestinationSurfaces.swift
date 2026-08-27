import SwiftUI
import VouchaAuth
import VouchaDesignSystem
import VouchaLocalization

struct NativeListSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel
    let onNavigateToTargetPath: (String) -> Void

    init(
        viewModel: NativeRouteSurfaceViewModel,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        self.viewModel = viewModel
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if viewModel.destination == .moderationTransparency {
                ModerationTransparencyRangePicker(
                    range: viewModel.moderationTransparencyRange,
                    onSelect: { nextRange in
                        onNavigateToTargetPath(
                            nextRange == .days30
                                ? "/moderation-transparency"
                                : "/moderation-transparency?range=\(nextRange.rawValue)"
                        )
                    }
                )
            }
            if viewModel.currentBookmarkCollection != nil || !viewModel.bookmarkRows.isEmpty {
                NativeBookmarkRowsSurface(
                    viewModel: viewModel,
                    onNavigateToTargetPath: onNavigateToTargetPath
                )
            } else {
                NativeRowsSurface(
                    rows: viewModel.rows,
                    state: viewModel.state,
                    retry: nativeRetryAction(for: viewModel),
                    onNavigate: onNavigateToTargetPath
                )
                if viewModel.destination == .moderationTransparency,
                   viewModel.moderationTransparencyRange == .all {
                    HybridPaginationControl(
                        hasMore: viewModel.moderationTransparencyNextCursor != nil,
                        isLoading: viewModel.moderationTransparencyIsLoadingOlder,
                        hasError: viewModel.moderationTransparencyLoadMoreError != nil
                    ) {
                        await viewModel.loadOlderModerationTransparency()
                    }
                }
                if viewModel.fediverseInstancePageInfo?.hasNextPage == true {
                    Button(UiMessages.string(
                        viewModel.fediverseInstancePaginationErrorMessage == nil
                            ? .nativeSwiftCommonLoadMore
                            : .nativeCommonRetry,
                        locale: nativeUiLocale
                    )) {
                        Task { await viewModel.loadMoreFediverseInstances() }
                    }
                    .buttonStyle(.bordered)
                    .disabled(viewModel.isLoadingMoreFediverseInstances)
                }
                if let errorMessage = viewModel.fediverseInstancePaginationErrorMessage {
                    Text(verbatim: UiMessages.string(errorMessage, locale: nativeUiLocale))
                        .foregroundStyle(.red)
                }
                HybridPaginationControl(
                    hasMore: viewModel.forwardPagination.hasLoadedPage
                        && viewModel.forwardPagination.hasMore,
                    isLoading: viewModel.forwardPagination.isLoading,
                    hasError: viewModel.forwardPagination.lastError != nil
                ) {
                    await viewModel.loadMoreForwardRows()
                }
                HybridPaginationControl(
                    hasMore: viewModel.crawlHistoryPagination.hasLoadedPage && viewModel.crawlHistoryPagination.hasMore,
                    isLoading: viewModel.crawlHistoryPagination.isLoading,
                    hasError: viewModel.crawlHistoryPagination.lastError != nil
                ) {
                    await viewModel.loadMoreCrawlHistory()
                }
                if viewModel.agentConversation != nil,
                   viewModel.agentConversationPageInfo?.hasNextPage == true {
                    Button(UiMessages.string(
                        viewModel.agentConversationPaginationErrorMessage == nil
                            ? .nativeSwiftDirectMessagesLoadOlderMessages
                            : .nativeCommonRetry,
                        locale: nativeUiLocale
                    )) {
                        Task { await viewModel.loadOlderAgentConversationMessages() }
                    }
                    .buttonStyle(.bordered)
                    .disabled(!viewModel.canLoadOlderAgentConversationMessages)
                }
            }

            if !viewModel.actions.isEmpty {
                NativeActionSurface(viewModel: viewModel)
            }
        }
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                if viewModel.destination == .notifications {
                    Button(UiMessages.string(.nativeSwiftNotificationsListSettings, locale: nativeUiLocale)) {
                        onNavigateToTargetPath("/my/notification-settings")
                    }
                }
            }
        }
    }
}

struct NativeSettingsSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(UiLocaleController.self)
    private var uiLocaleController
    @State
    private var settingsViewModel: SettingsViewModel
    private let onNavigateToTargetPath: (String) -> Void
    private let fediverseEnabled: Bool
    private let focusedSection: SettingsSurfaceFocusedSection?
    private let notificationActivationToken: Int
    private let notificationA11yActivator: NotificationA11yActivator

    init(
        viewModel: NativeRouteSurfaceViewModel,
        nativeOAuthAuthorizationCoordinator: NativeOAuthAuthorizationCoordinator? = nil,
        fediverseEnabled: Bool = false,
        focusedSection: SettingsSurfaceFocusedSection? = nil,
        a11yActivator: NotificationA11yActivator? = nil,
        notificationActivationToken: Int = 0,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in },
        onLogoutRequired: @escaping @MainActor () -> Void = {}
    ) {
        self.onNavigateToTargetPath = onNavigateToTargetPath
        self.fediverseEnabled = fediverseEnabled
        self.focusedSection = focusedSection
        self.notificationActivationToken = notificationActivationToken
        notificationA11yActivator = a11yActivator ?? .init()
        _settingsViewModel = State(
            initialValue: SettingsViewModel(
                client: viewModel.client,
                onLogoutRequired: onLogoutRequired,
                nativeOAuthAuthorizationCoordinator: nativeOAuthAuthorizationCoordinator
            )
        )
    }

    var body: some View {
        SettingsSurface(
            viewModel: settingsViewModel,
            fediverseEnabled: fediverseEnabled,
            focusedSection: focusedSection,
            a11yActivator: notificationA11yActivator,
            notificationActivationToken: notificationActivationToken,
            onNavigateToTargetPath: onNavigateToTargetPath
        )
        .task {
            settingsViewModel.uiLocaleController = uiLocaleController
        }
    }
}
