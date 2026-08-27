import SwiftUI
import VouchaDesignSystem

struct NativeSurfaceRow: View {
    @Environment(\.locale)
    private var locale
    @Environment(\.timeZone)
    private var timeZone
    let row: NativeRouteDestinationRow

    var body: some View {
        if let externalURL = row.externalURL {
            Link(destination: externalURL) {
                rowContent
            }
            .buttonStyle(.plain)
        } else {
            rowContent
        }
    }

    private var rowContent: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            Image(systemName: row.icon)
                .font(.headline)
                .foregroundStyle(Colors.primary)
                .frame(width: 22)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(row.localizedTitle(locale: locale, timeZone: timeZone))
                    .font(Typography.headline)
                Text(row.localizedDetail(locale: locale, timeZone: timeZone))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
            if row.externalURL != nil {
                Image(systemName: "arrow.up.forward")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}
