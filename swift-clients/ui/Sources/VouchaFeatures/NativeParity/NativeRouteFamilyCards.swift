import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeRouteFamilyCard: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let group: NativeRouteFamilyGroup

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.md) {
            Image(systemName: group.icon)
                .font(.title3)
                .foregroundStyle(Colors.primary)
                .frame(width: 24)
            VStack(alignment: .leading, spacing: Spacing.xs) {
                Text(UiMessages.string(group.title, locale: nativeUiLocale))
                    .font(Typography.headline)
                    .foregroundStyle(.primary)
                Text(UiMessages.string(group.summary, locale: nativeUiLocale))
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: Spacing.sm)
            Text(UiMessages.string(
                UiMessage(
                    .nativeSwiftRebasedRouteSurfacesRouteCount,
                    numberParameters: ["count": Double(group.entries.count)]
                ),
                locale: nativeUiLocale
            )).font(Typography.caption).foregroundStyle(Colors.secondaryLabel)
        }
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}
