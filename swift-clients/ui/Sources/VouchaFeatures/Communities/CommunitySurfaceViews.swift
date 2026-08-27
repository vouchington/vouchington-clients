import SwiftUI
import VouchaAPI
import VouchaCore
import VouchaDesignSystem
import VouchaLocalization

struct CommunityCreateSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: CommunityCreateViewModel
    let client: APIClient?
    let turnstileSiteKey: String?
    @State
    private var showingTurnstile = false

    init(viewModel: CommunityCreateViewModel, client: APIClient? = nil, turnstileSiteKey: String?) {
        _viewModel = State(initialValue: viewModel)
        self.client = client
        self.turnstileSiteKey = turnstileSiteKey
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            TextField(UiMessages.string(.nativeSwiftCommunitiesName, locale: nativeUiLocale), text: $viewModel.name)
                .textFieldStyle(.roundedBorder)
            TextField(UiMessages.string(.nativeSwiftCommunitiesSlug, locale: nativeUiLocale), text: $viewModel.slug)
                .textFieldStyle(.roundedBorder)
            NativeMarkdownEditor(client: client, markdown: $viewModel.markdown, minHeight: 140)

            HStack {
                Button(
                    UiMessages.string(
                        viewModel.turnstileToken == nil ? .nativeSwiftCommonVerify : .nativeAuthVerified,
                        locale: nativeUiLocale
                    ),
                    systemImage: "checkmark.shield"
                ) {
                    showingTurnstile = true
                }
                .buttonStyle(.bordered)

                Button(UiMessages.string(.nativeSwiftCommunitiesCreateCommunity, locale: nativeUiLocale)) {
                    Task { await viewModel.create() }
                }
                .buttonStyle(.borderedProminent)
                .disabled(!viewModel.canCreate)
            }

            stateText(viewModel.state, locale: nativeUiLocale)
        }
        .sheet(isPresented: $showingTurnstile) {
            NativeTurnstileChallengeView(
                siteKey: turnstileSiteKey ?? AppConfig.shared.requiredTurnstileSiteKey
            ) { token in
                viewModel.turnstileToken = token
                showingTurnstile = false
            }
        }
    }
}

struct CommunityWorkspaceSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: CommunityDetailViewModel
    var isSignedIn = true
    var showSignIn: () -> Void = {}

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(verbatim: UiMessages.string(viewModel.summary.title, locale: nativeUiLocale))
                    .font(Typography.largeTitle)
                Text(verbatim: UiMessages.string(viewModel.summary.detail, locale: nativeUiLocale))
                    .foregroundStyle(.secondary)
                Text(verbatim: UiMessages.string(viewModel.summary.activity, locale: nativeUiLocale))
                    .font(Typography.caption)

                if viewModel.visibleTabs.count > 1 {
                    Picker(
                        UiMessages.string(.nativeSwiftCommunitiesCommunityTabs, locale: nativeUiLocale),
                        selection: $viewModel.selectedTab
                    ) {
                        ForEach(viewModel.visibleTabs) { tab in
                            Text(UiMessages.string(tab.titleKey, locale: nativeUiLocale)).tag(tab)
                        }
                    }
                    .pickerStyle(.segmented)
                    .onChange(of: viewModel.selectedTab) { _, _ in
                        Task { await viewModel.load() }
                    }
                }

                CommunityActionRow(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
                CommunityTabControls(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
                CommunityDetailActionPanel(viewModel: viewModel, isSignedIn: isSignedIn, showSignIn: showSignIn)
                if viewModel.selectedTab == .moderationAnalytics {
                    ModerationTransparencyRangePicker(
                        range: viewModel.moderationTransparencyRange,
                        todayLabelKey: viewModel.canViewRawModerationAnalytics
                            ? .nativeSwiftGrowthDashboardToday
                            : .nativeSwiftCommunityRowsTransparencyLatestReleasedDay
                    ) { range in
                        Task { await viewModel.selectModerationTransparencyRange(range) }
                    }
                }

                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.summary.rows) { row in
                        NativeSurfaceRow(row: row)
                    }
                }

                CommunityTransparencyPaginationControl(viewModel: viewModel)

                if viewModel.selectedTab != .modmail {
                    HybridPaginationControl(
                        hasMore: viewModel.rowPagination.hasLoadedPage && viewModel.rowPagination.hasMore,
                        isLoading: viewModel.rowPagination.isLoading,
                        hasError: viewModel.rowPagination.lastError != nil
                    ) {
                        await viewModel.loadMoreRows()
                    }
                }

                HybridPaginationControl(
                    hasMore: viewModel.automodPagination.hasLoadedPage
                        && viewModel.automodPagination.hasMore,
                    isLoading: viewModel.automodPagination.isLoading,
                    hasError: viewModel.automodPagination.lastError != nil
                ) {
                    await viewModel.loadMoreCommunityAutomodActions()
                }

                if viewModel.selectedTab == .moderation {
                    HybridPaginationControl(
                        hasMore: viewModel.pendingReportPagination.hasLoadedPage
                            && viewModel.pendingReportPagination.hasMore,
                        isLoading: viewModel.pendingReportPagination.isLoading,
                        hasError: viewModel.pendingReportPagination.lastError != nil,
                        accessibilityIdentifier: "community-pending-reports-pagination"
                    ) {
                        await viewModel.loadMorePendingReports()
                    }
                }

                if viewModel.modmailThreadId == nil {
                    HybridPaginationControl(
                        hasMore: viewModel.modmailThreadPagination.hasLoadedPage
                            && viewModel.modmailThreadPagination.hasMore,
                        isLoading: viewModel.modmailThreadPagination.isLoading,
                        hasError: viewModel.modmailThreadPagination.lastError != nil
                    ) {
                        await viewModel.loadMoreModmail()
                    }
                } else if viewModel.canLoadMoreModmail {
                    Button(
                        UiMessages.string(
                            viewModel.modmailPaginationError == nil
                                ? .nativeSwiftCommonLoadMore
                                : .nativeCommonRetry,
                            locale: nativeUiLocale
                        ),
                        systemImage: "arrow.down.circle"
                    ) {
                        Task { await viewModel.loadMoreModmail() }
                    }
                    .disabled(viewModel.isLoadingMoreModmail)
                }

                if let paginationError = viewModel.modmailPaginationError {
                    Text(UiMessages.string(paginationError, locale: nativeUiLocale))
                        .font(Typography.caption)
                        .foregroundStyle(.red)
                }

                stateText(viewModel.state, locale: nativeUiLocale)
                if let status = viewModel.statusMessage {
                    Text(UiMessages.string(status, locale: nativeUiLocale)).font(Typography.caption)
                }
            }
        }
        .task { await viewModel.load() }
    }
}

@ViewBuilder
func stateText(_ state: CommunitySurfaceState, locale: Locale = Locale(identifier: "en")) -> some View {
    switch state {
    case .requiredTurnstile:
        Text(UiMessages.string(.nativeSwiftCommunitiesVerificationRequired, locale: locale)).font(Typography.caption)
    case let .error(message):
        Text(UiMessages.string(message, locale: locale)).font(Typography.caption).foregroundStyle(.red)
    default:
        EmptyView()
    }
}
