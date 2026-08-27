import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ModerationAppealsSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @State
    var viewModel: ModerationAppealsViewModel

    init(
        client: APIClient?,
        isSignedIn: Bool,
        isAdministrator: Bool,
        isSiteModerator: Bool
    ) {
        _viewModel = State(initialValue: ModerationAppealsViewModel(
            client: client,
            isSignedIn: isSignedIn,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator
        ))
    }

    init(viewModel: ModerationAppealsViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        if !viewModel.isSignedIn {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftModerationAppealsSignInMessage)
            )
        } else if !viewModel.canAccess {
            EmptyStateView(
                icon: "lock.shield",
                title: .message(.nativeSwiftModerationAppealsStaffAccessRequired),
                message: .message(.nativeSwiftModerationAppealsStaffAccessMessage)
            )
        } else {
            staffContent
                .task { await viewModel.load() }
        }
    }

    private var staffContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            Picker(
                UiMessages.string(.nativeSwiftModerationAppealsStatus, locale: nativeUiLocale),
                selection: statusBinding
            ) {
                ForEach([ModerationAppealStatus.pending, .resolved, .dismissed], id: \.self) { status in
                    Text(UiMessages.string(status.titleKey, locale: nativeUiLocale)).tag(status)
                }
            }
            .pickerStyle(.segmented)

            if let errorMessage = viewModel.errorMessage {
                Text(UiMessages.string(errorMessage, locale: nativeUiLocale))
                    .foregroundStyle(.red)
                    .accessibilityIdentifier("appeals-error")
            }

            if viewModel.isLoading, viewModel.appeals.isEmpty {
                ProgressView(UiMessages.string(.nativeSwiftModerationAppealsLoading, locale: nativeUiLocale))
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if viewModel.appeals.isEmpty {
                EmptyStateView(
                    icon: "arrow.uturn.left.circle",
                    title: .message(viewModel.selectedStatus.emptyTitleKey),
                    message: .message(.nativeSwiftModerationAppealsEmptyMessage)
                )
            } else {
                appealsList
            }
        }
        .padding(Spacing.md)
    }

    private var appealsList: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                ForEach(viewModel.appeals) { appeal in
                    ModerationAppealCard(viewModel: viewModel, appeal: appeal)
                }
                if let error = viewModel.loadMoreErrorMessage {
                    Text(UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
                }
                HybridPaginationControl(
                    hasMore: viewModel.appealPagination.hasMore,
                    isLoading: viewModel.appealPagination.isLoading,
                    hasError: viewModel.appealPagination.lastError != nil
                ) {
                    await viewModel.loadMore()
                }
            }
        }
    }

    private var statusBinding: Binding<ModerationAppealStatus> {
        Binding(
            get: { viewModel.selectedStatus },
            set: { status in Task { await viewModel.selectStatus(status) } }
        )
    }
}

extension ModerationAppealStatus {
    var emptyTitleKey: UiMessageKey {
        switch self {
        case .pending: .nativeSwiftModerationAppealsNoPendingAppeals
        case .resolved: .nativeSwiftModerationAppealsNoResolvedAppeals
        case .dismissed: .nativeSwiftModerationAppealsNoDismissedAppeals
        }
    }
}
