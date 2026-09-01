import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeBookmarkRowsSurface: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel
    let onNavigateToTargetPath: (String) -> Void

    var body: some View {
        switch viewModel.state {
        case .loading, .idle:
            ProgressView()
                .frame(maxWidth: .infinity)
                .padding(.vertical, Spacing.xl)
        case let .error(error):
            ErrorStateView(error: error) {
                await viewModel.load()
            }
        case .loaded where viewModel.bookmarkRows.isEmpty
            && !(viewModel.bookmarkPagination.hasLoadedPage
                && (viewModel.bookmarkPagination.hasMore || viewModel.bookmarkPagination.lastError != nil)):
            EmptyStateView(
                icon: "bookmark",
                title: .message(.nativeSwiftHouseholdsBookmarksNoBookmarks),
                message: .message(.nativeSwiftHouseholdsBookmarksBookmarkCollectionEmpty)
            )
        case .loaded:
            loadedRows
        }
    }

    private var loadedRows: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let message = viewModel.bookmarkMutationErrorMessage {
                Label(
                    UiMessages.string(message, locale: nativeUiLocale),
                    systemImage: "exclamationmark.triangle"
                )
                .font(Typography.subheadline)
                .foregroundStyle(.red)
                .accessibilityIdentifier("bookmark-collection-error")
            }
            ForEach(viewModel.bookmarkRows) { row in
                HStack(alignment: .center, spacing: Spacing.sm) {
                    Button {
                        Task {
                            if let path = await viewModel.bookmarkDestinationPath(for: row) {
                                onNavigateToTargetPath(path)
                            }
                        }
                    } label: {
                        NativeBookmarkRowContent(row: row)
                    }
                    .buttonStyle(.plain)

                    if let action = row.inverseAction {
                        Button(UiMessages.string(action.label, locale: nativeUiLocale)) {
                            Task { await viewModel.removeBookmarkRow(row) }
                        }
                        .buttonStyle(.bordered)
                        .disabled(viewModel.isRemovingBookmarkRow(row))
                        .accessibilityLabel(UiMessages.string(
                            .nativeSwiftHouseholdsBookmarksInverseActionAccessibility,
                            parameters: [
                                "action": UiMessages.string(action.label, locale: nativeUiLocale),
                                "title": UiMessages.string(row.title, locale: nativeUiLocale)
                            ],
                            locale: nativeUiLocale
                        ))
                    }
                }
                .padding(Spacing.md)
                .background(Colors.background.opacity(0.75))
                .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
                if let embed = viewModel.bookmarkEmbedsByEntityId[row.entityId] {
                    ProviderEmbedPreview(embed: embed)
                }
            }
            HybridPaginationControl(
                hasMore: viewModel.bookmarkPagination.hasLoadedPage
                    && viewModel.bookmarkPagination.hasMore,
                isLoading: viewModel.bookmarkPagination.isLoading,
                hasError: viewModel.bookmarkPagination.lastError != nil,
                isDisabled: !viewModel.inFlightBookmarkRowIds.isEmpty,
                accessibilityIdentifier: "bookmark-pagination-control"
            ) {
                await viewModel.loadMoreBookmarkRows()
            }
        }
    }
}

private struct NativeBookmarkRowContent: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let row: NativeBookmarkRow

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            Image(systemName: row.icon)
                .font(.headline)
                .foregroundStyle(Colors.primary)
                .frame(width: 22)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(row.title, locale: nativeUiLocale))
                    .font(Typography.headline)
                Text(UiMessages.string(row.detail, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
        }
        .contentShape(Rectangle())
    }
}
