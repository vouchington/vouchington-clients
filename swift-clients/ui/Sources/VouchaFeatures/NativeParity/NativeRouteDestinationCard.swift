import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeRouteDestinationCard: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(alignment: .top, spacing: Spacing.sm) {
                Image(systemName: entry.destinationIdentifier?.rawValueIcon ?? "arrow.forward.circle")
                    .font(.headline)
                    .foregroundStyle(Colors.primary)
                    .frame(width: 20)

                VStack(alignment: .leading, spacing: 2) {
                    if let familyText = entry.presentationFamilyText {
                        Text(UiMessages.string(familyText, locale: nativeUiLocale))
                            .font(Typography.headline)
                    }
                    Text(verbatim: UiMessages.string(
                        .protocolValue(destinationIdentifier),
                        locale: nativeUiLocale
                    ))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
                }
            }

            Text(entry.representativePath)
                .font(Typography.subheadline.monospaced())
                .foregroundStyle(Colors.secondaryLabel)
                .fixedSize(horizontal: false, vertical: true)

            DisclosureGroup(UiMessages.string(.nativeSwiftRouteFamilyDirectoryPatterns, locale: nativeUiLocale)) {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    ForEach(entry.patterns, id: \.template) { pattern in
                        Text(pattern.template)
                            .font(Typography.caption.monospaced())
                            .foregroundStyle(Colors.secondaryLabel)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .padding(.top, Spacing.xs)
            }
        }
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.75))
        .clipShape(RoundedRectangle(cornerRadius: 8, style: .continuous))
    }

    private var destinationIdentifier: String {
        entry.destinationIdentifier?.rawValue
            ?? UiMessages.string(.nativeSwiftRouteFamilyDirectoryUnmapped, locale: nativeUiLocale)
    }
}
