import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct LandingPageAnalyticsSection: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: LandingPagesViewModel

    var body: some View {
        Section(header: Text(verbatim: UiMessages.string(.nativeSwiftLandingPagesAnalytics, locale: nativeUiLocale))) {
            if viewModel.isAnalyticsPaidAccessRequired {
                Text(UiMessages.string(.nativeSwiftLandingPagesAnalyticsPaidAccessRequired, locale: nativeUiLocale))
                    .foregroundStyle(Colors.secondaryLabel)
            } else {
                switch viewModel.selectedPageAnalyticsState {
                case .idle, .loading:
                    HStack(spacing: Spacing.sm) {
                        ProgressView()
                        Text(UiMessages.string(.nativeSwiftLandingPagesLoadingAnalytics, locale: nativeUiLocale))
                    }
                case let .error(error):
                    VStack(alignment: .leading, spacing: Spacing.sm) {
                        Text(verbatim: error.errorDescription ?? UiMessages.string(
                            .nativeSwiftLandingPagesAnalyticsUnavailable,
                            locale: nativeUiLocale
                        ))
                        .foregroundStyle(Colors.negativeVote)
                        Button(UiMessages.string(.nativeSwiftCommonTryAgain, locale: nativeUiLocale)) {
                            Task { await viewModel.reloadSelectedPageAnalytics() }
                        }
                    }
                case .loaded:
                    if let analytics = viewModel.selectedPageAnalytics {
                        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                            analyticsRow(
                                icon: "chart.line.uptrend.xyaxis",
                                title: .nativeSwiftRouteSurfaceVisits,
                                detail: .count(analytics.totalVisits, item: "visit")
                            )
                            analyticsRow(
                                icon: "cursorarrow.click",
                                title: .nativeSwiftRouteSurfaceClicks,
                                detail: .count(analytics.totalClicks, item: "click")
                            )
                            analyticsRow(
                                icon: "person.2",
                                title: .nativeSwiftRouteSurfaceUniqueVisitors,
                                detail: .count(analytics.uniqueVisitors, item: "visitor")
                            )
                        }
                    } else {
                        Text(UiMessages.string(.nativeSwiftLandingPagesAnalyticsUnavailable, locale: nativeUiLocale))
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                }
            }
        }
    }

    private func analyticsRow(icon: String, title: UiMessageKey, detail: UiVerbatimText) -> some View {
        NativeSurfaceRow(row: .init(icon: icon, title: .message(title), detail: detail))
    }
}
