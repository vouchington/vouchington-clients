import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct GrowthDashboardSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: GrowthDashboardViewModel

    init(client: APIClient?, initialRange: GrowthRange = .thirtyDays, initialMetrics: GrowthMetrics? = nil) {
        _viewModel = State(initialValue: GrowthDashboardViewModel(
            client: client,
            initialRange: initialRange,
            initialMetrics: initialMetrics
        ))
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            rangePicker
            content
        }
        .task { await viewModel.loadIfNeeded() }
    }

    private var rangePicker: some View {
        Picker(UiMessages.string(.nativeSwiftGrowthDashboardRange, locale: nativeUiLocale), selection: rangeBinding) {
            Text(UiMessages.string(.nativeSwiftGrowthDashboardToday, locale: nativeUiLocale)).tag(GrowthRange.today)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage7d, locale: nativeUiLocale))
                .tag(GrowthRange.sevenDays)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage30d, locale: nativeUiLocale))
                .tag(GrowthRange.thirtyDays)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage90d, locale: nativeUiLocale))
                .tag(GrowthRange.ninetyDays)
            Text(UiMessages.string(.nativeSwiftCommonAll, locale: nativeUiLocale)).tag(GrowthRange.all)
        }
        .pickerStyle(.segmented)
    }

    private var rangeBinding: Binding<GrowthRange> {
        Binding(
            get: { viewModel.range },
            set: { nextRange in
                Task { await viewModel.selectRange(nextRange) }
            }
        )
    }

    @ViewBuilder
    private var content: some View {
        if case let .error(error) = viewModel.state {
            ErrorStateView(error: error) { await viewModel.load() }
        } else if let metrics = viewModel.metrics {
            metricsContent(metrics)
                .opacity(viewModel.isLoading ? 0.6 : 1)
        } else if viewModel.isLoading {
            ProgressView()
                .frame(maxWidth: .infinity, minHeight: 180)
        } else {
            EmptyStateView(
                icon: "chart.line.uptrend.xyaxis",
                title: .message(.nativeSwiftEmptyStateGrowthMetricsUnavailable),
                message: .message(.nativeSwiftEmptyStateGrowthMetricsUnavailableMessage)
            )
        }
    }

}
