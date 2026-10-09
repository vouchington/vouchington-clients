import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityBrowseSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    var viewModel: CommunityBrowseViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftCommunitiesSearchCommunities, locale: nativeUiLocale),
                    text: $viewModel.query
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftCommonSearch, locale: nativeUiLocale)) {
                    Task { await viewModel.search(query: viewModel.query) }
                }
                .buttonStyle(.borderedProminent)
            }

            ScrollView {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(viewModel.rows) { row in
                        NativeSurfaceRow(row: .init(
                            icon: "person.3",
                            title: .verbatim(row.title),
                            detail: .joined([.verbatim(row.detail), row.metrics]),
                            provenance: row.provenance
                        ))
                    }

                    HybridPaginationControl(
                        hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                        isLoading: viewModel.pagination.isLoading,
                        hasError: viewModel.pagination.lastError != nil
                    ) {
                        await viewModel.loadNextPage()
                    }
                }
            }
        }
        .task { await viewModel.load() }
    }
}
