import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ListsView {
    var listPicker: some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.lists) { list in
                Button {
                    Task { await viewModel.selectList(list) }
                } label: {
                    ListsSummaryRow(list: list, isSelected: viewModel.selectedList?.id == list.id)
                }
                .buttonStyle(.plain)
            }
            HybridPaginationControl(
                hasMore: viewModel.listPagination.hasLoadedPage && viewModel.listPagination.hasMore,
                isLoading: viewModel.listPagination.isLoading,
                hasError: viewModel.listPagination.lastError != nil
            ) {
                await viewModel.loadMoreLists()
            }
        }
    }

    @ViewBuilder
    var selectedListItems: some View {
        switch viewModel.itemState {
        case .loading:
            LoadingView(label: UiMessages.string(.nativeSwiftListsLoadingItems, locale: nativeUiLocale))
        case let .error(error):
            ErrorStateView(error: error) {
                if let selected = viewModel.selectedList {
                    await viewModel.selectList(selected)
                }
            }
        default:
            if viewModel.items.isEmpty {
                EmptyStateView(
                    icon: "tray",
                    title: .message(.nativeSwiftEmptyStateNoListItems),
                    message: .message(.nativeSwiftEmptyStateNoListItemsMessage)
                )
            } else {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.items) { item in
                        ListsItemRow(item: item)
                    }
                    HybridPaginationControl(
                        hasMore: viewModel.itemPagination.hasLoadedPage && viewModel.itemPagination.hasMore,
                        isLoading: viewModel.itemPagination.isLoading,
                        hasError: viewModel.itemPagination.lastError != nil
                    ) {
                        await viewModel.loadMoreItems()
                    }
                }
            }
        }
    }
}
