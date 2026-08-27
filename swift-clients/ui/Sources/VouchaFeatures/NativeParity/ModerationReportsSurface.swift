import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct ModerationReportsSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: ModerationReportsViewModel
    let isSignedIn: Bool
    let onNavigate: (String) -> Void
    let showSignIn: () -> Void

    init(
        client: APIClient?,
        isSignedIn: Bool,
        viewerTier: ModerationReportsViewerTier,
        onNavigate: @escaping (String) -> Void,
        showSignIn: @escaping () -> Void
    ) {
        self.isSignedIn = isSignedIn
        self.onNavigate = onNavigate
        self.showSignIn = showSignIn
        _viewModel = State(initialValue: ModerationReportsViewModel(client: client, viewerTier: viewerTier))
    }

    init(
        viewModel: ModerationReportsViewModel,
        isSignedIn: Bool,
        onNavigate: @escaping (String) -> Void,
        showSignIn: @escaping () -> Void
    ) {
        self.isSignedIn = isSignedIn
        self.onNavigate = onNavigate
        self.showSignIn = showSignIn
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        if !isSignedIn {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftModerationReportsSignInReviewReports)
            )
            Button(UiMessages.string(.nativeSwiftEmptyStateSignIn, locale: nativeUiLocale), action: showSignIn)
                .buttonStyle(.borderedProminent)
        } else {
            reportsContent
                .task { await viewModel.load() }
                .confirmationDialog(
                    UiMessages.string(
                        viewModel.pendingConfirmation?.title
                            ?? .message(.nativeSwiftModerationReportsConfirmAction),
                        locale: nativeUiLocale
                    ),
                    isPresented: Binding(
                        get: { viewModel.pendingConfirmation != nil },
                        set: {
                            if !$0 {
                                viewModel.pendingConfirmation = nil
                            }
                        }
                    ),
                    titleVisibility: .visible
                ) {
                    if let confirmation = viewModel.pendingConfirmation {
                        Button(
                            UiMessages.string(confirmation.buttonTitle, locale: nativeUiLocale),
                            role: .destructive
                        ) {
                            run(confirmation)
                        }
                    }
                    Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                        viewModel.pendingConfirmation = nil
                    }
                }
        }
    }

    private var reportsContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            ModerationReportsFilters(viewModel: viewModel)
            if let notice = viewModel.notice {
                Label(UiMessages.string(notice, locale: nativeUiLocale), systemImage: "info.circle")
                    .foregroundStyle(.secondary)
            }
            if let error = viewModel.loadError {
                Label(UiMessages.string(error, locale: nativeUiLocale), systemImage: "exclamationmark.triangle")
                    .foregroundStyle(Colors.negativeVote)
                Button(UiMessages.string(.nativeSwiftModerationReportsTryAgain, locale: nativeUiLocale)) {
                    Task { await viewModel.load() }
                }
            } else if viewModel.isLoading {
                ProgressView(UiMessages.string(
                    .nativeSwiftModerationReportsLoadingReports,
                    locale: nativeUiLocale
                ))
            } else {
                reportList
            }
            if let error = viewModel.paginationError {
                Label(UiMessages.string(error, locale: nativeUiLocale), systemImage: "exclamationmark.triangle")
                    .foregroundStyle(Colors.negativeVote)
            }
            if viewModel.isRefreshingGrouped {
                ProgressView(UiMessages.string(
                    .nativeSwiftModerationReportsRefreshingReports,
                    locale: nativeUiLocale
                ))
            }
            HybridPaginationControl(
                hasMore: viewModel.hasNextPage,
                isLoading: viewModel.reportPagination.isLoading,
                hasError: viewModel.reportPagination.lastError != nil
            ) {
                await viewModel.loadMore()
            }
            if !viewModel.selectedReportIds.isEmpty {
                selectedActionErrors
                bulkActions
            }
        }
    }

}

enum ModerationReportsConfirmation {
    case bulkDismiss, bulkRemove, clusterDismiss(String), report(ModerationReportAction, String)

    var title: UiVerbatimText {
        .message(.nativeSwiftModerationReportsConfirmationTitle)
    }

    var buttonTitle: UiVerbatimText {
        switch self {
        case .bulkDismiss, .clusterDismiss: .message(.nativeSwiftModerationReportsDismissReports)
        case .bulkRemove: .message(.nativeSwiftModerationReportsRemoveContent)
        case let .report(action, _): .message(action.confirmationTitleKey)
        }
    }
}
