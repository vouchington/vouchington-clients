import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct IntegrityPenaltyLedgerSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: IntegrityPenaltyLedgerViewModel
    @State
    private var pendingRevoke: IntegrityPenaltyRow?
    let onNavigate: (String) -> Void

    init(
        client: APIClient?,
        viewerTier: IntegrityViewerTier,
        domain: IntegrityDomain,
        onNavigate: @escaping (String) -> Void
    ) {
        _viewModel = State(initialValue: IntegrityPenaltyLedgerViewModel(
            service: client.map(APIIntegrityPenaltyService.init(client:)),
            viewerTier: viewerTier,
            domain: domain
        ))
        self.onNavigate = onNavigate
    }

    init(
        viewModel: IntegrityPenaltyLedgerViewModel,
        pendingRevoke: IntegrityPenaltyRow? = nil,
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: viewModel)
        _pendingRevoke = State(initialValue: pendingRevoke)
        self.onNavigate = onNavigate
    }

    var body: some View {
        if !viewModel.canAccess {
            IntegrityAccessGate(viewerTier: viewModel.viewerTier)
        } else {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(text(viewModel.domain == .report
                        ? .nativeSwiftIntegrityReportPenaltiesTitle
                        : .nativeSwiftIntegrityVotePenaltiesTitle))
                    .font(Typography.largeTitle)
                IntegritySectionNavigation(domain: viewModel.domain, selectedPenalties: true, onNavigate: onNavigate)
                Picker(
                    text(.nativeSwiftIntegrityStatus),
                    selection: statusBinding
                ) {
                    ForEach(IntegrityPenaltyStatusFilter.allCases, id: \.self) { status in
                        Text(text(status.titleKey)).tag(status)
                    }
                }
                .pickerStyle(.segmented)
                content
            }
            .padding(Spacing.md)
            .task { await viewModel.load() }
            .confirmationDialog(
                text(.nativeSwiftIntegrityConfirmAction),
                isPresented: Binding(
                    get: { pendingRevoke != nil },
                    set: {
                        if !$0 {
                            pendingRevoke = nil
                        }
                    }
                )
            ) {
                Button(text(.nativeSwiftIntegrityRevoke), role: .destructive) {
                    guard let penalty = pendingRevoke else { return }
                    pendingRevoke = nil
                    Task { await viewModel.revoke(penalty) }
                }
                Button(text(.nativeSwiftIntegrityCancel), role: .cancel) { pendingRevoke = nil }
            } message: {
                Text(text(.nativeSwiftIntegrityConfirmRevoke))
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        if viewModel.isLoading, viewModel.penalties.isEmpty {
            ProgressView(text(.nativeSwiftIntegrityLoading))
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if let error = viewModel.initialError, viewModel.penalties.isEmpty {
            EmptyStateView(
                icon: "exclamationmark.triangle",
                title: .message(error.titleKey),
                message: .message(error.messageKey),
                actionTitle: .message(.nativeSwiftIntegrityRetry),
                action: { Task { await viewModel.load() } }
            )
        } else if viewModel.penalties.isEmpty {
            EmptyStateView(
                icon: "checkmark.shield",
                title: .message(.nativeSwiftIntegrityNoPenalties),
                message: .message(.nativeSwiftIntegrityNoPenaltiesMessage)
            )
        } else {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.md) {
                    ForEach(viewModel.penalties) { penalty in
                        IntegrityPenaltyCard(
                            viewModel: viewModel,
                            penalty: penalty,
                            onNavigate: onNavigate,
                            onRequestRevoke: { pendingRevoke = penalty }
                        )
                    }
                    continuationControls
                }
            }
        }
    }

    @ViewBuilder
    private var continuationControls: some View {
        if let error = viewModel.continuationError {
            Text(text(error.messageKey)).foregroundStyle(.red)
            Button(text(.nativeSwiftIntegrityRetryLoadingMore)) { Task { await viewModel.loadMore() } }
        }
        if viewModel.hasMore {
            Button(text(viewModel.isLoadingMore
                    ? .nativeSwiftIntegrityLoadingMore
                    : .nativeSwiftIntegrityLoadMore)) {
                Task { await viewModel.loadMore() }
            }
            .disabled(viewModel.isLoadingMore)
            .frame(maxWidth: .infinity)
        }
    }

    private var statusBinding: Binding<IntegrityPenaltyStatusFilter> {
        Binding(
            get: { viewModel.selectedStatus },
            set: { status in Task { await viewModel.selectStatus(status) } }
        )
    }

    private func text(_ key: UiMessageKey) -> String {
        UiMessages.string(key, locale: nativeUiLocale)
    }
}

private extension IntegrityPenaltyStatusFilter {
    var titleKey: UiMessageKey {
        switch self {
        case .active: .nativeSwiftIntegrityActive
        case .revoked: .nativeSwiftIntegrityRevoked
        case .all: .nativeSwiftIntegrityAll
        }
    }
}

extension IntegrityLedgerError {
    var titleKey: UiMessageKey {
        switch self {
        case .reportUnavailable: .nativeSwiftIntegrityReportUnavailable
        case .scopeUnavailable: .nativeSwiftIntegrityScopeUnavailable
        default: .nativeSwiftIntegrityLoadFailed
        }
    }

    var messageKey: UiMessageKey {
        switch self {
        case .loadFailed: .nativeSwiftIntegrityLoadFailed
        case .loadMoreFailed: .nativeSwiftIntegrityLoadMoreFailed
        case .reportUnavailable: .nativeSwiftIntegrityReportUnavailable
        case .scopeUnavailable: .nativeSwiftIntegrityScopeUnavailable
        case .revokeFailed: .nativeSwiftIntegrityRevokeFailed
        case .resultUncertain: .nativeSwiftIntegrityResultUncertain
        case .reconciliationFailed: .nativeSwiftIntegrityReconciliationFailed
        }
    }
}
