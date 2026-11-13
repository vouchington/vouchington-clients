import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension PostsListView {
    var postFilters: some View {
        VStack {
            filterPicker
            if !viewModel.isFeed, !timeRangeOptions.isEmpty {
                PostTimeRangePicker(
                    range: viewModel.selectedTimeRange,
                    options: timeRangeOptions.filter { viewModel.timeRangeOptions.contains($0) }
                ) { range in
                    Task { await viewModel.selectTimeRange(range) }
                }
            }
        }
    }

    private var filterPicker: some View {
        Picker(UiMessages.string(.nativeSwiftListsFilter, locale: nativeUiLocale), selection: $viewModel.filter) {
            ForEach(PostFilter.allCases) { filter in
                Text(UiMessages.string(filter.titleKey, locale: nativeUiLocale)).tag(filter)
            }
        }
        .pickerStyle(.segmented)
        .padding(.horizontal, Spacing.md)
    }
}
