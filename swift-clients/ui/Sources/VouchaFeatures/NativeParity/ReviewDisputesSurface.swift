import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ReviewDisputesSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: ReviewDisputesViewModel

    init(
        client: APIClient?,
        isSignedIn: Bool,
        isAdministrator: Bool,
        isSiteModerator: Bool
    ) {
        _viewModel = State(initialValue: ReviewDisputesViewModel(
            client: client,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator
        ))
    }

    init(viewModel: ReviewDisputesViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    @State
    private var selectedSummary: ReviewDisputeSummary?

    var body: some View {
        if !viewModel.isSignedIn {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftReviewDisputesSignInMessage)
            )
        } else if !viewModel.canAccess {
            EmptyStateView(
                icon: "lock.shield",
                title: .message(.nativeSwiftReviewDisputesStaffAccessRequired),
                message: .message(.nativeSwiftReviewDisputesStaffAccessMessage)
            )
        } else {
            staffContent.task { await viewModel.load() }
        }
    }

    private var staffContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Picker(
                UiMessages.string(.nativeSwiftReviewDisputesStatus, locale: nativeUiLocale),
                selection: statusBinding
            ) {
                ForEach([ReviewDisputeStatus.pending, .resolved, .dismissed], id: \.self) { status in
                    Text(UiMessages.string(status.titleKey, locale: nativeUiLocale)).tag(status)
                }
            }
            .pickerStyle(.segmented)

            if let error = viewModel.errorMessage {
                Text(UiMessages.string(error, locale: nativeUiLocale))
                    .foregroundStyle(.red)
                    .accessibilityIdentifier("review-disputes-error")
                Button(UiMessages.string(.nativeCommonRetry, locale: nativeUiLocale)) {
                    Task { await viewModel.load() }
                }
            }

            if viewModel.isLoading, viewModel.summaryPagination.items.isEmpty {
                ProgressView(UiMessages.string(.nativeSwiftReviewDisputesLoading, locale: nativeUiLocale))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if viewModel.summaryPagination.items.isEmpty {
                EmptyStateView(
                    icon: "exclamationmark.bubble",
                    title: .message(viewModel.selectedStatus.disputeEmptyTitleKey),
                    message: .message(.nativeSwiftReviewDisputesEmptyMessage)
                )
            } else {
                disputeList
            }
        }
        .padding(Spacing.md)
    }

    private var disputeList: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                ForEach(viewModel.summaryPagination.items) { dispute in
                    Button { selectedSummary = dispute } label: {
                        StaffModerationSummaryRow(dispute: dispute)
                    }
                    .buttonStyle(.plain)
                }
                if let error = viewModel.loadMoreErrorMessage {
                    Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
                }
                HybridPaginationControl(
                    hasMore: viewModel.summaryPagination.hasMore,
                    isLoading: viewModel.summaryPagination.isLoading,
                    hasError: viewModel.summaryPagination.lastError != nil
                ) {
                    await viewModel.loadMore()
                }
            }
        }
        .sheet(item: $selectedSummary, content: { summary in
            ReviewDisputeDetail(viewModel: viewModel, id: summary.id)
        })
    }

    private var statusBinding: Binding<ReviewDisputeStatus> {
        Binding(
            get: { viewModel.selectedStatus },
            set: { status in Task { await viewModel.selectStatus(status) } }
        )
    }
}

private extension ReviewDisputeStatus {
    var disputeEmptyTitleKey: UiMessageKey {
        switch self {
        case .pending: .nativeSwiftReviewDisputesNoPending
        case .resolved: .nativeSwiftReviewDisputesNoResolved
        case .dismissed: .nativeSwiftReviewDisputesNoDismissed
        }
    }
}
