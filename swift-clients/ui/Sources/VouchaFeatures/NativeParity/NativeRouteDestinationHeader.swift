import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeRouteDestinationHeader: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let entry: NativeRouteCatalogEntry

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if let familyText = entry.presentationFamilyText {
                Label(
                    UiMessages.string(familyText, locale: nativeUiLocale),
                    systemImage: entry.destinationIdentifier?.rawValueIcon ?? "arrow.forward.circle"
                )
                .font(Typography.headline)
            }
            Text(entry.representativePath)
                .font(Typography.subheadline.monospaced())
                .foregroundStyle(Colors.secondaryLabel)
                .fixedSize(horizontal: false, vertical: true)
            if let destination = entry.destinationIdentifier {
                Text(verbatim: UiMessages.string(
                    .protocolValue(destination.rawValue),
                    locale: nativeUiLocale
                ))
                .font(Typography.caption.monospaced())
                .foregroundStyle(Colors.secondaryLabel)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.md)
        .background(Colors.background)
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }
}
