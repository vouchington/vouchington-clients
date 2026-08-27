import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeAiCostsSurface: View {
    @Environment(\.locale) private var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    let isAdministrator: Bool
    @State private var viewModel: NativeAiCostsViewModel?

    init(entry: NativeRouteCatalogEntry, client: APIClient?, isAdministrator: Bool) {
        self.entry = entry
        self.isAdministrator = isAdministrator
        _viewModel = State(initialValue: client.map(NativeAiCostsViewModel.init(client:)))
    }

    var body: some View {
        if viewModel == nil {
            EmptyStateView(
                icon: "lock",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue)
            )
        } else if !isAdministrator {
            EmptyStateView(
                icon: "lock",
                title: .message(.nativeSwiftEmptyStateAdministratorRequired)
            )
        } else if let viewModel {
            content(viewModel)
        }
    }

    func content(_ viewModel: NativeAiCostsViewModel) -> some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: Spacing.md) {
                NativeRouteDestinationHeader(entry: entry)
                Text(UiMessages.string(
                    .extractedAiCostsPagePerCommunityLlmModerationUsage4d9a2c81,
                    locale: nativeUiLocale
                ))
                .foregroundStyle(.secondary)
                if viewModel.rows.isEmpty, case .loading = viewModel.state {
                    ProgressView().frame(maxWidth: .infinity, minHeight: 180)
                } else if viewModel.rows.isEmpty, case let .error(error) = viewModel.state {
                    ErrorStateView(error: error) { await viewModel.refresh() }
                } else if viewModel.rows.isEmpty {
                    EmptyStateView(
                        icon: "chart.bar.xaxis",
                        title: .message(.extractedAiCostsPageAiCosts75cce222),
                        message: .message(.extractedAiCostsPageNoAiUsageRecordedYet87b1e2ea)
                    )
                } else {
                    if let refreshError = viewModel.refreshError {
                        ErrorStateView(error: refreshError) { await viewModel.refresh() }
                    }
                    ForEach(viewModel.rows) { row in
                        rowCard(row)
                    }
                    HybridPaginationControl(
                        hasMore: !viewModel.isRefreshing
                            && viewModel.refreshError == nil
                            && viewModel.pagination.hasLoadedPage
                            && viewModel.pagination.hasMore,
                        isLoading: viewModel.pagination.isLoading,
                        hasError: viewModel.pagination.lastError != nil,
                        accessibilityIdentifier: "native-ai-costs-pagination"
                    ) { await viewModel.loadNextPage() }
                }
            }
            .padding(Spacing.md)
        }
        .refreshable { await viewModel.refresh() }
        .task { await viewModel.loadIfNeeded() }
    }

    private func rowCard(_ row: CommunityAiCostTotal) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(row.communitySlug).font(.headline)
            Grid(alignment: .leading, horizontalSpacing: Spacing.md, verticalSpacing: Spacing.xs) {
                metric(.extractedAiCostsPageRequestsAda27592, value: number(row.requestCount))
                metric(.extractedAiCostsPageInputTokensC54a41f6, value: number(row.totalInputTokens))
                metric(.extractedAiCostsPageOutputTokensA0a1074f, value: number(row.totalOutputTokens))
                metric(
                    .extractedAiCostsPageTotalCostUsdEed03fbf,
                    value: ScaledMoneyAggregateFormatter.format(row.totalCost, locale: nativeUiLocale)
                )
            }
            if row.unpricedRequestCount > 0 {
                Text(verbatim: UiMessages.plural(
                    .extractedAiCostsPageUnpricedRequestCount,
                    value: row.unpricedRequestCount,
                    locale: nativeUiLocale
                ))
                .font(.footnote)
                .foregroundStyle(.secondary)
            }
        }
        .padding(Spacing.md)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.thinMaterial, in: RoundedRectangle(cornerRadius: 12))
    }

    private func metric(_ key: UiMessageKey, value: UiVerbatimText) -> some View {
        GridRow {
            Text(UiMessages.string(key, locale: nativeUiLocale)).foregroundStyle(.secondary)
            Text(UiMessages.string(value, locale: nativeUiLocale))
        }
    }

    private func number(_ value: Int64) -> UiVerbatimText {
        .verbatim(UiMessages.number(value, locale: nativeUiLocale))
    }
}
