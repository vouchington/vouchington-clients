import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct GrowthMetricRow: Identifiable {
    let id = UUID()
    let label: UiVerbatimText
    let value: UiVerbatimText
    let detail: UiVerbatimText

    init(_ label: UiVerbatimText, _ value: UiVerbatimText, _ detail: UiVerbatimText) {
        self.label = label
        self.value = value
        self.detail = detail
    }
}

struct GrowthMetricSection: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let title: UiVerbatimText
    let rows: [GrowthMetricRow]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(verbatim: UiMessages.string(title, locale: nativeUiLocale)).font(Typography.headline)
            LazyVGrid(columns: [.init(.adaptive(minimum: 150), spacing: Spacing.sm)], spacing: Spacing.sm) {
                ForEach(rows) { row in
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(verbatim: UiMessages.string(row.label, locale: nativeUiLocale))
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                        Text(verbatim: UiMessages.string(row.value, locale: nativeUiLocale)).font(Typography.headline)
                        Text(verbatim: UiMessages.string(row.detail, locale: nativeUiLocale))
                            .font(Typography.caption)
                            .foregroundStyle(Colors.secondaryLabel)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(Spacing.sm)
                    .background(Colors.background)
                    .overlay(
                        RoundedRectangle(cornerRadius: 8)
                            .stroke(Colors.separator, lineWidth: 1)
                    )
                    .clipShape(RoundedRectangle(cornerRadius: 8))
                }
            }
        }
    }
}

struct GrowthMiniBars: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let title: UiVerbatimText
    let values: [Int]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(verbatim: UiMessages.string(title, locale: nativeUiLocale)).font(Typography.headline)
            HStack(alignment: .bottom, spacing: 3) {
                ForEach(Array(values.enumerated()), id: \.offset) { _, value in
                    RoundedRectangle(cornerRadius: 2)
                        .fill(Colors.primary)
                        .frame(height: barHeight(value))
                }
            }
            .frame(height: 72)
        }
    }

    private func barHeight(_ value: Int) -> CGFloat {
        guard let maximum = values.max(), maximum > 0 else { return 4 }
        return Swift.max(4, CGFloat(value) / CGFloat(maximum) * 72)
    }
}
