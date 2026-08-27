import SwiftUI
import VouchaLocalization
import VouchaModels

struct ModerationTransparencyRangePicker: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let range: ModerationTransparencyRange
    let todayLabelKey: UiMessageKey
    let onSelect: (ModerationTransparencyRange) -> Void

    init(
        range: ModerationTransparencyRange,
        todayLabelKey: UiMessageKey = .nativeSwiftCommunityRowsTransparencyLatestReleasedDay,
        onSelect: @escaping (ModerationTransparencyRange) -> Void
    ) {
        self.range = range
        self.todayLabelKey = todayLabelKey
        self.onSelect = onSelect
    }

    var body: some View {
        Picker(
            UiMessages.string(.nativeSwiftGrowthDashboardRange, locale: nativeUiLocale),
            selection: rangeBinding
        ) {
            Text(UiMessages.string(todayLabelKey, locale: nativeUiLocale))
                .tag(ModerationTransparencyRange.today)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage7d, locale: nativeUiLocale))
                .tag(ModerationTransparencyRange.days7)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage30d, locale: nativeUiLocale))
                .tag(ModerationTransparencyRange.days30)
            Text(UiMessages.string(.nativeSwiftGrowthDashboardMessage90d, locale: nativeUiLocale))
                .tag(ModerationTransparencyRange.days90)
            Text(UiMessages.string(.nativeSwiftCommonAll, locale: nativeUiLocale))
                .tag(ModerationTransparencyRange.all)
        }
        .pickerStyle(.segmented)
    }

    private var rangeBinding: Binding<ModerationTransparencyRange> {
        Binding(
            get: { range },
            set: { nextRange in
                onSelect(nextRange)
            }
        )
    }
}
