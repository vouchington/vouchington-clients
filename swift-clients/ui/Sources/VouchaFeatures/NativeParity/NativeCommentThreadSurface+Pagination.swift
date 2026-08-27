import SwiftUI
import VouchaDesignSystem

extension NativeCommentThreadSurface {
    var descendantPaginationControl: some View {
        HybridPaginationControl(
            hasMore: viewModel.descendantPagination.hasLoadedPage && viewModel.descendantPagination.hasMore,
            isLoading: viewModel.descendantPagination.isLoading,
            hasError: viewModel.descendantPagination.lastError != nil,
            accessibilityIdentifier: "comment-descendants-pagination"
        ) {
            await viewModel.loadMoreDescendants()
        }
    }
}
