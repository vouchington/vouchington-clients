import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeEngineeringAgentsSurface: View {
    @Environment(\.locale)
    private var locale
    @Bindable var viewModel: NativeRouteSurfaceViewModel
    let isSignedIn: Bool
    let isAdministrator: Bool
    let onNavigate: (String) -> Void
    let showSignIn: () -> Void

    var body: some View {
        Group {
            if !isSignedIn {
                EmptyStateView(
                    icon: "lock",
                    title: .message(.nativeSwiftEmptyStateSignInRequired),
                    message: .message(.nativeAuthSignInToContinue),
                    actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                    action: showSignIn
                )
            } else if !isAdministrator {
                EmptyStateView(
                    icon: "lock",
                    title: .message(.nativeSwiftEmptyStateAdministratorRequired)
                )
            } else {
                content
            }
        }
        .task(id: isSignedIn && isAdministrator) {
            guard isSignedIn, isAdministrator else { return }
            await viewModel.load()
        }
    }

    @ViewBuilder private var content: some View {
        switch viewModel.state {
        case .loading where !hasLoadedAgentContent:
            ProgressView().frame(maxWidth: .infinity, alignment: .center)
        case let .error(error) where !hasLoadedAgentContent:
            ErrorStateView(error: error) { await viewModel.load() }
        default:
            if viewModel.agentConversation != nil {
                transcript
            } else if viewModel.agentDetail != nil {
                detail
            } else {
                directory
            }
        }
    }

    private var hasLoadedAgentContent: Bool {
        viewModel.agentConversation != nil
            || viewModel.agentDetail != nil
            || !viewModel.agentDirectoryResults.isEmpty
    }

    private var directory: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            rows(viewModel.agentDirectoryRows())
            continuation(
                hasMore: viewModel.canLoadMoreAgents,
                isLoading: viewModel.isLoadingMoreAgents,
                error: viewModel.agentDirectoryPaginationErrorMessage,
                action: viewModel.loadMoreAgents
            )
        }
    }

    private var detail: some View {
        NativeEngineeringAgentDetailSurface(viewModel: viewModel, onNavigate: onNavigate)
    }

    private var transcript: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            rows(viewModel.agentConversationRows(
                fallbackId: viewModel.agentConversation?.id ?? "",
                agentSystemUserId: viewModel.agentConversationAgentSystemUserId
            ))
            if viewModel.agentConversationMessages.isEmpty {
                EmptyStateView(
                    icon: "square.stack.3d.down.right",
                    title: .message(.nativeSwiftRouteSurfaceNoResults)
                )
            }
            continuation(
                hasMore: viewModel.canLoadOlderAgentConversationMessages,
                isLoading: viewModel.isLoadingOlderAgentConversationMessages,
                error: viewModel.agentConversationPaginationErrorMessage,
                action: viewModel.loadOlderAgentConversationMessages
            )
        }
    }

    @ViewBuilder
    private func rows(_ rows: [NativeRouteDestinationRow]) -> some View {
        if rows.isEmpty {
            EmptyStateView(
                icon: "square.stack.3d.down.right",
                title: .message(.nativeSwiftRouteSurfaceNoResults)
            )
        } else {
            NativeRowsSurface(rows: rows, state: .loaded, retry: nil, onNavigate: onNavigate)
        }
    }

    private func continuation(
        hasMore: Bool,
        isLoading: Bool,
        error: UiVerbatimText?,
        action: @escaping @MainActor () async -> Void
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if hasMore || error != nil {
                Button(UiMessages.string(
                    error == nil ? .nativeSwiftCommonLoadMore : .nativeCommonRetry,
                    locale: locale
                )) {
                    Task { await action() }
                }
                .disabled(isLoading)
            }
            if let error {
                Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
            }
        }
    }

}
