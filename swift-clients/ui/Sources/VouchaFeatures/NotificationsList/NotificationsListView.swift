import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct NotificationsListView: View {
    @Environment(\.locale)
    var nativeUiLocale
    public let viewModel: NotificationsListViewModel
    public let onNavigateToTargetPath: (String) -> Void
    @State
    private var isNavigating = false

    public init(
        viewModel: NotificationsListViewModel,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        self.viewModel = viewModel
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

    public var body: some View {
        Group {
            if viewModel.items.isEmpty {
                emptyOrLoadingView
            } else {
                loadedListView
            }
        }
        .navigationTitle(UiMessages.string(.nativeSwiftNotificationsListNotifications, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .refreshable { await viewModel.reload() }
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                Button(UiMessages.string(.nativeSwiftNotificationsListMarkAllRead, locale: nativeUiLocale)) {
                    Task { await viewModel.markAllRead() }
                }
                .disabled(viewModel.items.isEmpty)
            }
            ToolbarItem(placement: .primaryAction) {
                Button(UiMessages.string(.nativeSwiftNotificationsListSettings, locale: nativeUiLocale)) {
                    onNavigateToTargetPath("/my/notification-settings")
                }
            }
        }
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
            EmptyStateView(
                icon: "bell",
                title: .message(.nativeSwiftEmptyStateNoNotifications),
                message: .message(.nativeSwiftEmptyStateNoNotificationsMessage)
            )
        }
    }

    private var loadedListView: some View {
        List {
            ForEach(viewModel.items) { notification in
                let isRead = viewModel.isRead(notification)
                NotificationCard(notification: notification, isRead: isRead)
                    .opacity(isRead ? 0.6 : 1.0)
                    .contentShape(Rectangle())
                    .onTapGesture {
                        guard !isNavigating else { return }
                        isNavigating = true
                        Task {
                            defer { isNavigating = false }
                            if let targetPath = await viewModel.activate(notification: notification) {
                                onNavigateToTargetPath(targetPath)
                            }
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
}
