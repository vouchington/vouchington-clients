import SwiftUI
import VouchaLocalization
import VouchaModels

struct PointValuationEditor: View {
    @Environment(\.locale)
    private var locale
    @Binding
    var draft: PointValuationDraft
    let isSaving: Bool
    let save: () -> Void
    let cancel: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            TextField(
                UiMessages.string(
                    .extractedPointValuationsManagerValuationListValuePerPointFa9d9f8a,
                    locale: locale
                ),
                text: $draft.valuePerPointText
            )
            Picker(
                UiMessages.string(.nativeSwiftCommonCurrency, locale: locale),
                selection: $draft.currency
            ) {
                ForEach(Currency.supported) { currency in
                    Text(currency.code.uppercased()).tag(currency.code)
                }
            }
            TextField(
                UiMessages.string(
                    .extractedPointValuationsManagerValuationListOptionalNote951ddd37,
                    locale: locale
                ),
                text: $draft.note,
                axis: .vertical
            )
            HStack {
                Button(
                    UiMessages.string(
                        .extractedPointValuationsManagerValuationListSave1509f561,
                        locale: locale
                    ),
                    action: save
                )
                .buttonStyle(.borderedProminent)
                .disabled(isSaving)
                Button(
                    UiMessages.string(
                        .extractedPointValuationsManagerValuationListCancel19766ed6,
                        locale: locale
                    ),
                    action: cancel
                )
                .disabled(isSaving)
            }
        }
    }
}
