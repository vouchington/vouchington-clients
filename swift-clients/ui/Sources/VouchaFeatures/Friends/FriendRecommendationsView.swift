import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct FriendRecommendationsRouteSurface: View {
    let client: APIClient?
    let isSignedIn: Bool
    let onNavigateToTargetPath: (String) -> Void
    let showSignIn: () -> Void

    var body: some View {
        if let client, isSignedIn {
            FriendRecommendationsView(
                viewModel: FriendRecommendationsViewModel(client: client),
                onNavigateToTargetPath: onNavigateToTargetPath
            )
        } else {
            EmptyStateView(
                icon: "person.2",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        }
    }
}

public struct FriendRecommendationsView: View {
    @Environment(\.locale)
    private var locale
    @State
    private var viewModel: FriendRecommendationsViewModel
    private let onNavigateToTargetPath: (String) -> Void

    public init(
        viewModel: FriendRecommendationsViewModel,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: viewModel)
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

    public var body: some View {
        Group {
            if !viewModel.recommendations.isEmpty || viewModel.hasMore {
                recommendationsList
            } else {
                emptyOrLoading
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftRouteFamilyDirectoryRecommendationsTitle, locale: locale))
        .task {
            if !viewModel.pagination.hasLoadedPage {
                await viewModel.load()
            }
        }
        .refreshable { await viewModel.reload() }
    }

    private var recommendationsList: some View {
        List {
            ForEach(viewModel.recommendations) { recommendation in
                FriendRecommendationRow(
                    recommendation: recommendation,
                    user: viewModel.user(for: recommendation),
                    avatarURL: viewModel.avatarURL(for: recommendation),
                    isMutating: viewModel.isMutating(userId: recommendation.id),
                    onFollow: { Task { await viewModel.follow(recommendation) } },
                    onDismiss: { Task { await viewModel.dismiss(recommendation) } }
                )
            }
            if let mutationErrorMessage = viewModel.mutationErrorMessage {
                Text(verbatim: UiMessages.string(mutationErrorMessage, locale: locale))
                    .foregroundStyle(.red)
            }
            HybridPaginationControl(
                hasMore: viewModel.hasMore,
                isLoading: viewModel.isLoadingMore,
                hasError: viewModel.hasPaginationError
            ) {
                await viewModel.loadMore()
            }
            .listRowSeparator(.hidden)
            dismissedButton
                .listRowSeparator(.hidden)
        }
        .listStyle(.plain)
    }

    @ViewBuilder
    private var emptyOrLoading: some View {
        switch viewModel.state {
        case .idle, .loading:
            LoadingView()
        case let .error(error):
            ErrorStateView(error: error) {
                await viewModel.reload()
            }
        case .loaded:
            VStack(spacing: Spacing.md) {
                EmptyStateView(
                    icon: "person.2",
                    title: .message(.nativeSwiftRouteFamilyDirectoryRecommendationsTitle),
                    actionTitle: .message(
                        viewModel.hasConnectedProvider
                            ? .nativeCommonRetry
                            : .nativeSwiftSettingsAccount
                    ),
                    action: {
                        if viewModel.hasConnectedProvider {
                            Task { await viewModel.reload() }
                        } else {
                            onNavigateToTargetPath("/my/identity")
                        }
                    }
                )
                dismissedButton
            }
        }
    }

    private var dismissedButton: some View {
        Button(UiMessages.string(.nativeSwiftModerationReportsDismissed, locale: locale)) {
            onNavigateToTargetPath("/my/friend-recommendations/dismissed")
        }
        .buttonStyle(.bordered)
    }
}
