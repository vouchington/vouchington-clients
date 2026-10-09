import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ListsSummaryRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    let list: UserList
    let isSelected: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(list.name).font(Typography.headline)
            ProvenanceBadge(provenance: list.provenance)
            Text(list.description
                ?? UiMessages.string(list.visibility.titleKey, locale: nativeUiLocale))
                .font(Typography.body)
                .foregroundStyle(Colors.secondaryLabel)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.md)
        .background(isSelected ? Colors.primary.opacity(0.12) : Colors.background.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}

struct ListsItemRow: View {
    @Environment(\.locale)
    var nativeUiLocale
    let item: ListItem

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(item.entityId).font(Typography.headline)
            Text(verbatim: UiMessages.string(
                .joined([
                    .message(item.itemType.titleKey),
                    listItemMediaTypeText(item.mediaType)
                ]),
                locale: nativeUiLocale
            )).font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.md)
        .background(Colors.background.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: 8))
    }
}
