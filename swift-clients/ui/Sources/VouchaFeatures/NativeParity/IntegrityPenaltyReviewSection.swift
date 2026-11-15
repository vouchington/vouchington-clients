import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct IntegrityPenaltyReviewSection: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: IntegrityPenaltyReviewViewModel
    let onNavigate: (String) -> Void

    init(
        viewModel: IntegrityPenaltyReviewViewModel,
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: viewModel)
        self.onNavigate = onNavigate
    }

    var body: some View {
        if viewModel.canAccess {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                HStack {
                    Text(text(.moderationPenaltyReviewsTitle)).font(Typography.headline)
                    Spacer()
                    Button(text(.moderationPenaltyReviewsRefresh)) { Task { await viewModel.load() } }
                        .disabled(viewModel.isLoading)
                }
                Text(text(.moderationPenaltyReviewsDescription)).font(Typography.caption)
                content
            }
            .task { await viewModel.load() }
        } else if viewModel.permissionLost {
            Text(text(.nativeSwiftIntegrityAdministratorRequired)).foregroundStyle(.red)
        }
    }

    @ViewBuilder
    private var content: some View {
        if viewModel.initialError != nil {
            Text(text(.moderationPenaltyReviewsLoadFailed)).foregroundStyle(.red)
        }
        if viewModel.isLoading, viewModel.penalties.isEmpty {
            ProgressView(text(.nativeSwiftIntegrityLoading))
        } else if viewModel.penalties.isEmpty, !viewModel.hasMore {
            if viewModel.initialError == nil {
                Text(text(.moderationPenaltyReviewsEmpty))
            }
        } else {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.penalties) { penalty in
                        IntegrityPenaltyReviewCard(viewModel: viewModel, penalty: penalty, onNavigate: onNavigate)
                    }
                    continuation
                }
            }
            .frame(maxHeight: 320)
        }
    }

    @ViewBuilder
    private var continuation: some View {
        if viewModel.continuationError != nil {
            Text(text(.nativeSwiftIntegrityLoadMoreFailed)).foregroundStyle(.red)
        }
        if viewModel.hasMore {
            Text(text(.moderationPenaltyReviewsMore))
            Button(text(viewModel.isLoadingMore
                    ? .nativeSwiftIntegrityLoadingMore
                    : .nativeSwiftIntegrityLoadMore)) {
                Task { await viewModel.loadMore() }
            }
            .disabled(viewModel.isLoadingMore)
        }
    }

    private func text(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}
