import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeCommentThreadSurface {
    var ancestorPaginationControl: some View {
        Group {
            if viewModel.ancestorPagination.hasLoadedPage,
               viewModel.ancestorPagination.hasMore || viewModel.ancestorPagination.lastError != nil {
                Button {
                    Task { await viewModel.loadMoreAncestors() }
                } label: {
                    HStack(spacing: Spacing.sm) {
                        if viewModel.ancestorPagination.isLoading {
                            ProgressView()
                                .controlSize(.small)
                        }
                        Text(UiMessages.string(ancestorPaginationLabelKey, locale: nativeUiLocale))
                    }
                    .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.plain)
                .disabled(viewModel.ancestorPagination.isLoading)
                .accessibilityIdentifier("comment-ancestors-pagination")
            }
        }
    }

    private var ancestorPaginationLabelKey: UiMessageKey {
        if viewModel.ancestorPagination.isLoading {
            return .nativeSwiftCommonLoadingMore
        }
        if viewModel.ancestorPagination.lastError != nil {
            return .nativeCommonRetry
        }
        return .extractedCommentsCommentAncestorTrailShowEarlierReplies56b87971
    }

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
