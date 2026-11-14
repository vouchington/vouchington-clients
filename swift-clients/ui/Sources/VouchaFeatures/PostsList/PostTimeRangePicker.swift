import SwiftUI
import VouchaLocalization

/// A native dropdown whose available windows and default are supplied by its post-list context.
struct PostTimeRangePicker: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let range: PostTimeRange
    let options: [PostTimeRange]
    let onSelect: (PostTimeRange) -> Void

    init(
        range: PostTimeRange,
        options: [PostTimeRange] = PostTimeRange.listOptions,
        defaultRange: PostTimeRange = .last30Days,
        onSelect: @escaping (PostTimeRange) -> Void
    ) {
        self.range = options.contains(range) ? range
            : options.contains(defaultRange) ? defaultRange : options.first ?? defaultRange
        self.options = options
        self.onSelect = onSelect
    }

    var body: some View {
        Picker(
            UiMessages.string(.extractedPostsPostTimeRangeSelectLabel, locale: nativeUiLocale),
            selection: Binding(get: { range }, set: onSelect)
        ) {
            ForEach(options, id: \.self) { option in
                Text(UiMessages.string(option.titleKey, locale: nativeUiLocale)).tag(option)
            }
        }
        .pickerStyle(.menu)
        .accessibilityIdentifier("post-time-range-picker")
    }
}

extension PostTimeRange {
    public static let listOptions: [Self] = [.lastWeek, .last30Days, .lastYear, .allTime]

    var titleKey: UiMessageKey {
        switch self {
        case .lastWeek: .extractedPostsPostTimeRangeSelectLast7Days
        case .last30Days: .extractedPostsPostTimeRangeSelectLast30Days
        case .lastYear: .extractedPostsPostTimeRangeSelectLastYear
        case .allTime: .extractedPostsPostTimeRangeSelectAllTime
        }
    }
}
