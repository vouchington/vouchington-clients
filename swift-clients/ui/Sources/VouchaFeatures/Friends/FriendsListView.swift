import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct FriendsListView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: FriendsListViewModel

    public init(viewModel: FriendsListViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        Group {
            if viewModel.shouldRenderList {
                loadedListView
            } else {
                emptyOrLoadingView
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftFriendsFriends, locale: nativeUiLocale))
        .task(id: viewModel.tab) { await viewModel.load() }
        .refreshable { await viewModel.reload() }
    }

    @ViewBuilder
    private var emptyOrLoadingView: some View {
        switch viewModel.state {
        case .loading:
            VStack {
                tabPicker
                LoadingView()
            }
        case let .error(error):
            VStack {
                tabPicker
                ErrorStateView(error: error) {
                    await viewModel.reload()
                }
            }
        default:
            VStack {
                tabPicker
                EmptyStateView(
                    icon: "person.2",
                    title: .message(
                        viewModel.tab == .following
                            ? .nativeSwiftFriendsNoFollowing
                            : .nativeSwiftFriendsNoFollowers
                    ),
                    message: .message(
                        viewModel.tab == .following
                            ? .nativeSwiftFriendsNoFollowingMessage
                            : .nativeSwiftFriendsNoFollowersMessage
                    )
                )
            }
        }
    }

    private var loadedListView: some View {
        List {
            tabPicker
                .listRowSeparator(.hidden)
            ForEach(viewModel.items) { user in
                HStack {
                    UserRow(user: user, avatarURL: viewModel.avatarURL(for: user))
                    Spacer()
                    followButton(for: user)
                }
            }
            HybridPaginationControl(
                hasMore: viewModel.hasMore,
                isLoading: viewModel.isLoadingMore,
                hasError: viewModel.hasPaginationError
            ) {
                await viewModel.loadMore()
            }
            .listRowSeparator(.hidden)
        }
        .listStyle(.plain)
    }

    private var tabPicker: some View {
        Picker(UiMessages.string(.nativeSwiftFriendsTab, locale: nativeUiLocale), selection: $viewModel.tab) {
            ForEach(FriendsTab.allCases) { tab in
                Text(UiMessages.string(tab.titleKey, locale: nativeUiLocale)).tag(tab)
            }
        }
        .pickerStyle(.segmented)
        .padding(.horizontal, Spacing.md)
    }
}
