import SwiftUI
import VouchaLocalization
import VouchaModels

struct SpendingCategoryEditor: View {
    @Environment(\.locale) private var locale
    @Binding var draft: SpendingCategoryDraft
    let isSaving: Bool
    let save: () -> Void
    let cancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            TextField(
                UiMessages.string(.extractedSpendingCategoriesManagerCategoryListAmount49e96d7c, locale: locale),
                text: Binding(
                    get: { draft.amountText },
                    set: { draft.updateAmountText($0) }
                )
            )
            Picker(
                UiMessages.string(.nativeSwiftCommonCurrency, locale: locale),
                selection: $draft.currency
            ) {
                ForEach(Currency.supported) { currency in
                    Text(currency.code.uppercased()).tag(currency.code)
                }
            }
            Picker(
                UiMessages.string(.extractedSpendingCategoriesManagerCategoryListFrequency16b6668d, locale: locale),
                selection: $draft.frequency
            ) {
                Text(UiMessages.string(
                    .extractedSpendingCategoriesManagerFrequencySelectMonthly9b11f6b7,
                    locale: locale
                )).tag(SpendingFrequency.monthly)
                Text(UiMessages.string(
                    .extractedSpendingCategoriesManagerFrequencySelectAnnually1ec9d1d5,
                    locale: locale
                )).tag(SpendingFrequency.annually)
            }
            TextField(
                UiMessages.string(.extractedSpendingCategoriesManagerCategoryListOptionalNote951ddd37, locale: locale),
                text: $draft.note,
                axis: .vertical
            )
            HStack {
                Button(
                    UiMessages.string(.extractedSpendingCategoriesManagerCategoryListSave1509f561, locale: locale),
                    action: save
                ).buttonStyle(.borderedProminent).disabled(isSaving)
                Button(
                    UiMessages.string(.extractedSpendingCategoriesManagerCategoryListCancel19766ed6, locale: locale),
                    action: cancel
                ).disabled(isSaving)
            }
        }
        .onChange(of: locale.identifier) { _, _ in
            draft.applyLocale(locale)
        }
    }
}
