import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeDetailSurface {
    @ViewBuilder
    var destinationRows: some View {
        if showsRows {
            NativeRowsSurface(
                rows: viewModel.rows,
                state: viewModel.state,
                retry: nativeRetryAction(for: viewModel),
                onNavigate: onNavigate
            )
            if viewModel.rows.isEmpty, !viewModel.isLoading {
                EmptyStateView(
                    icon: entry.destinationIdentifier?.rawValueIcon ?? "square.dashed",
                    title: entry.presentationFamilyText
                        ?? .message(.nativeSwiftRouteSurfaceMatchedRoute),
                    message: .verbatim(entry.representativePath)
                )
            }
            HybridPaginationControl(
                hasMore: viewModel.crawlHistoryPagination.hasLoadedPage && viewModel.crawlHistoryPagination.hasMore,
                isLoading: viewModel.crawlHistoryPagination.isLoading,
                hasError: viewModel.crawlHistoryPagination.lastError != nil,
                accessibilityIdentifier: "crawl-history-pagination-control"
            ) {
                await viewModel.loadMoreCrawlHistory()
            }
        }
    }
}
