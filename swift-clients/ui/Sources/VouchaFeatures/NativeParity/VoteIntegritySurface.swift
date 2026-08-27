import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct VoteIntegritySurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: VoteIntegrityViewModel
    @State
    private var pendingPenaltyFlag: VoteIntegrityFlag?
    let onNavigate: (String) -> Void

    init(
        client: APIClient?,
        viewerTier: IntegrityViewerTier,
        onNavigate: @escaping (String) -> Void
    ) {
        _viewModel = State(initialValue: VoteIntegrityViewModel(
            service: client.map(APIVoteIntegrityService.init(client:)),
            viewerTier: viewerTier
        ))
        self.onNavigate = onNavigate
    }

    init(
        viewModel: VoteIntegrityViewModel,
        pendingPenaltyFlag: VoteIntegrityFlag? = nil,
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: viewModel)
        _pendingPenaltyFlag = State(initialValue: pendingPenaltyFlag)
        self.onNavigate = onNavigate
    }

    var body: some View {
        if !viewModel.canAccess {
            IntegrityAccessGate(viewerTier: viewModel.viewerTier)
        } else {
            VStack(alignment: .leading, spacing: Spacing.md) {
                IntegritySectionNavigation(domain: .vote, selectedPenalties: false, onNavigate: onNavigate)
                Text(UiMessages.string(.nativeSwiftIntegrityVoteFlagsTitle, locale: nativeUiLocale))
                    .font(Typography.largeTitle)
                IntegrityStatusPicker(selectedStatus: viewModel.selectedStatus) { status in
                    Task { await viewModel.selectStatus(status) }
                }
                if viewModel.initialErrorMessage != nil, !viewModel.flags.isEmpty {
                    Text(UiMessages.string(.nativeSwiftIntegrityLoadFailed, locale: nativeUiLocale))
                        .foregroundStyle(.red)
                    Button(
                        UiMessages.string(.nativeSwiftIntegrityRetry, locale: nativeUiLocale),
                        action: { Task { await viewModel.load() } }
                    )
                }
                IntegrityInitialState(
                    isLoading: viewModel.isLoading,
                    hasError: viewModel.initialErrorMessage != nil,
                    isEmpty: viewModel.flags.isEmpty,
                    onRetry: { Task { await viewModel.load() } }
                )
                if !viewModel.flags.isEmpty {
                    ScrollView {
                        LazyVStack(alignment: .leading, spacing: Spacing.md) {
                            ForEach(viewModel.flags) { flag in
                                VoteIntegrityFlagCard(
                                    viewModel: viewModel,
                                    flag: flag,
                                    onNavigate: onNavigate,
                                    onRequestPenalty: { pendingPenaltyFlag = flag }
                                )
                            }
                            IntegrityContinuationControls(
                                hasMore: viewModel.hasMore,
                                isLoadingMore: viewModel.isLoadingMore,
                                hasError: viewModel.continuationErrorMessage != nil,
                                onLoadMore: { Task { await viewModel.loadMore() } }
                            )
                        }
                    }
                }
            }
            .padding(Spacing.md)
            .task { await viewModel.load() }
            .confirmationDialog(
                UiMessages.string(.nativeSwiftIntegrityConfirmAction, locale: nativeUiLocale),
                isPresented: Binding(
                    get: { pendingPenaltyFlag != nil },
                    set: {
                        if !$0 {
                            pendingPenaltyFlag = nil
                        }
                    }
                )
            ) {
                Button(
                    UiMessages.string(.nativeSwiftIntegrityApplyVotePenalty, locale: nativeUiLocale),
                    role: .destructive
                ) {
                    guard let flag = pendingPenaltyFlag else { return }
                    pendingPenaltyFlag = nil
                    Task { await viewModel.applyPenalty(flag) }
                }
                Button(
                    UiMessages.string(.nativeSwiftIntegrityCancel, locale: nativeUiLocale),
                    role: .cancel
                ) { pendingPenaltyFlag = nil }
            } message: {
                Text(UiMessages.string(.nativeSwiftIntegrityApplyVotePenalty, locale: nativeUiLocale))
            }
        }
    }
}
