import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct PostsListView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: PostsListViewModel
    public let isSignedIn: Bool
    public let canVote: Bool
    public let currentUserId: String?
    public let hideDownCount: Bool
    public let showSignIn: (() -> Void)?

    public init(
        viewModel: PostsListViewModel,
        isSignedIn: Bool = true,
        canVote: Bool? = nil,
        currentUserId: String? = nil,
        hideDownCount: Bool = false,
        showSignIn: (() -> Void)? = nil
    ) {
        self.viewModel = viewModel
        self.isSignedIn = isSignedIn
        self.canVote = canVote ?? isSignedIn
        self.currentUserId = currentUserId
        self.hideDownCount = hideDownCount
        self.showSignIn = showSignIn
    }

    public var body: some View {
        Group {
            if viewModel.items.isEmpty {
                emptyOrLoadingView
            } else {
                loadedListView
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftPostsListPosts, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .refreshable { await viewModel.reload() }
        .emailVerificationRecovery(
            client: viewModel.client,
            gate: viewModel.emailVerificationGate
        )
    }

    @ViewBuilder
    private var emptyOrLoadingView: some View {
        switch viewModel.state {
        case .loading:
            LoadingView()
        case let .error(error):
            ErrorStateView(error: error) {
                await viewModel.reload()
            }
        default:
            VStack {
                filterPicker
                EmptyStateView(
                    icon: "bubble.left.and.bubble.right",
                    title: .message(.nativeSwiftEmptyStateNoPosts),
                    message: .message(.nativeSwiftEmptyStateNoPostsMessage)
                )
            }
        }
    }

    private var loadedListView: some View {
        List {
            filterPicker
                .listRowSeparator(.hidden)
            ForEach(viewModel.items) { post in
                PostCard(
                    post: post,
                    variant: .compact,
                    myVote: viewModel.myVotesByPostId[post.id],
                    canCreateVote: canVote,
                    onVote: isSignedIn
                        ? { choice in
                            Task { await viewModel.vote(postId: post.id, choice: choice) }
                        }
                        : nil,
                    onSignedOutTap: isSignedIn ? nil : showSignIn,
                    hideDownCount: hideDownCount,
                    isSaved: viewModel.isSaved(postId: post.id),
                    isHidden: viewModel.isHidden(postId: post.id),
                    onToggleSaved: isSignedIn ?
                        { _ = Task<Void, Never> { await viewModel.toggleSave(postId: post.id) } } : nil,
                    onToggleHidden: isSignedIn ?
                        { _ = Task<Void, Never> { await viewModel.toggleHide(postId: post.id) } } : nil
                )
                if canDistribute(post), let currentUserId {
                    HStack {
                        Spacer()
                        FollowerDistributionActions(
                            client: viewModel.client,
                            currentUserId: currentUserId,
                            target: .post(post.id)
                        )
                    }
                }
            }
            HybridPaginationControl(
                hasMore: viewModel.hasMore,
                isLoading: viewModel.isLoading,
                hasError: viewModel.hasPaginationError
            ) {
                await viewModel.loadNextPage()
            }
            .listRowSeparator(.hidden)
        }
        .listStyle(.plain)
    }

    private var filterPicker: some View {
        Picker(UiMessages.string(.nativeSwiftListsFilter, locale: nativeUiLocale), selection: $viewModel.filter) {
            ForEach(PostFilter.allCases) { filter in
                Text(UiMessages.string(filter.titleKey, locale: nativeUiLocale)).tag(filter)
            }
        }
        .pickerStyle(.segmented)
        .padding(.horizontal, Spacing.md)
    }

    private func canDistribute(_ post: Post) -> Bool {
        canDistributeToFollowers(post, currentUserId: currentUserId, isSignedIn: isSignedIn)
    }
}
