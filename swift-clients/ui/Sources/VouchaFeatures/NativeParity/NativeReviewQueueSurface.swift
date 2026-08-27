import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeReviewQueueSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Environment(\.timeZone)
    private var nativeUiTimeZone
    @Bindable
    var viewModel: NativeReviewQueueViewModel
    var inspectionDidAppear: ((Self) -> Void)?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if let errorMessage = viewModel.errorMessage, viewModel.state != .error {
                Text(UiMessages.string(errorMessage, locale: nativeUiLocale))
                    .foregroundStyle(.red)
                    .accessibilityIdentifier("review-queue-error")
            }
            content
            if viewModel.state == .loaded || viewModel.state == .empty {
                controls
            }
        }
        .onAppear { inspectionDidAppear?(self) }
        .task(id: loadTaskIdentity) { await viewModel.load() }
        .onDisappear { viewModel.cancelListOperations() }
    }

    var loadTaskIdentity: UUID {
        viewModel.loadTaskId
    }

    @ViewBuilder
    private var content: some View {
        switch viewModel.state {
        case .signInRequired:
            queueEmptyState(
                "person.crop.circle.badge.exclamationmark",
                .nativeSwiftEmptyStateSignInRequired,
                .nativeSwiftModerationReportsReviewQueueSignInMessage
            )
        case .administratorRequired:
            queueEmptyState(
                "lock.shield",
                .nativeSwiftModerationReportsReviewQueueAdministratorRequired,
                .nativeSwiftModerationReportsReviewQueueAdministratorMessage
            )
        case .idle, .loading:
            ProgressView(localized(.nativeSwiftModerationReportsReviewQueueLoading))
        case .error:
            EmptyStateView(
                icon: "exclamationmark.triangle",
                title: .message(.nativeSwiftModerationReportsReviewQueueUnavailable),
                message: viewModel.errorMessage
                    ?? .message(.nativeSwiftModerationReportsReviewQueueTryAgain)
            )
            Button(localized(.nativeSwiftModerationReportsReviewQueueRetry)) {
                Task { await viewModel.load() }
            }
            .buttonStyle(.borderedProminent)
        case .empty:
            queueEmptyState(
                "checkmark.circle",
                .nativeSwiftModerationReportsReviewQueueEmpty,
                .nativeSwiftModerationReportsReviewQueueEmptyMessage
            )
        case .loaded:
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                ForEach(viewModel.items) { item in
                    NativeReviewQueueRow(
                        item: item,
                        timeZone: nativeUiTimeZone,
                        viewModel: viewModel
                    )
                }
            }
        }
    }

    private var controls: some View {
        HStack(spacing: Spacing.sm) {
            Button(localized(.nativeSwiftModerationReportsReviewQueueRefresh)) {
                Task { await viewModel.refresh() }
            }
            .disabled(!viewModel.canRefresh)
            HybridPaginationControl(
                hasMore: viewModel.hasNextPage,
                isLoading: viewModel.pagination.isLoading,
                hasError: viewModel.pagination.lastError != nil,
                isDisabled: !viewModel.canLoadMore
            ) {
                await viewModel.loadMore()
            }
            if viewModel.isListLoading {
                ProgressView()
            }
        }
    }

    private func queueEmptyState(_ icon: String, _ title: UiMessageKey, _ message: UiMessageKey) -> some View {
        EmptyStateView(icon: icon, title: .message(title), message: .message(message))
    }

    private func localized(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}
