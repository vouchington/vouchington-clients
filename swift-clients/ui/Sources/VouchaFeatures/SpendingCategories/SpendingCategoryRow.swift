import SwiftUI
import VouchaLocalization
import VouchaModels

struct SpendingCategoryRow: View {
    @Environment(\.locale) private var locale
    let category: SpendingCategory
    let viewModel: SpendingCategoriesViewModel
    @State private var interactionState: SpendingCategoryRowInteractionState

    init(
        category: SpendingCategory,
        viewModel: SpendingCategoriesViewModel,
        interactionState: SpendingCategoryRowInteractionState? = nil
    ) {
        self.category = category
        self.viewModel = viewModel
        _interactionState = State(
            initialValue: interactionState ?? SpendingCategoryRowInteractionState(category: category)
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(verbatim: UiMessages.string(.userContent(category.spendingCategory.name), locale: locale))
                .font(.headline)
            Text(verbatim: UiMessages.string(amountAndFrequencyText, locale: locale))
            if let note = category.note {
                Text(verbatim: UiMessages.string(.userContent(note), locale: locale))
            }
            if !category.canManage {
                Text(
                    UiMessages.string(
                        .extractedSpendingCategoriesManagerCategorySummaryHouseholdReadOnly7898,
                        locale: locale
                    )
                )
                .foregroundStyle(.secondary)
            } else if interactionState.isEditing {
                SpendingCategoryEditor(
                    draft: $interactionState.draft,
                    isSaving: viewModel.mutatingIds.contains(category.id),
                    save: {
                        Task {
                            if await viewModel.save(category: category, draft: interactionState.draft) {
                                interactionState.isEditing = false
                            }
                        }
                    },
                    cancel: { interactionState.draft = SpendingCategoryDraft(category: category, locale: locale)
                        interactionState.isEditing = false
                    }
                )
            } else {
                HStack {
                    Button(UiMessages.string(
                        .extractedSpendingCategoriesManagerCategorySummaryEdit464c4ffd,
                        locale: locale
                    )) { interactionState.draft = SpendingCategoryDraft(category: category, locale: locale)
                        interactionState.isEditing = true
                    }
                    Button(
                        UiMessages
                            .string(.extractedSpendingCategoriesManagerCategorySummaryRemoveC3812fc4, locale: locale),
                        role: .destructive
                    ) { interactionState.confirmsDeletion = true }.disabled(viewModel.mutatingIds.contains(category.id))
                }
            }
        }
        .padding(.vertical, 8)
        .confirmationDialog(
            UiMessages.string(.extractedSpendingCategoriesManagerCategorySummaryRemove9fe2f243, locale: locale),
            isPresented: $interactionState.confirmsDeletion
        ) {
            Button(
                UiMessages.string(.extractedSpendingCategoriesManagerCategorySummaryConfirmEebdd24a, locale: locale),
                role: .destructive
            ) { Task { _ = await viewModel.delete(category) } }
            Button(
                UiMessages.string(.extractedSpendingCategoriesManagerCategorySummaryCancel19766ed6, locale: locale),
                role: .cancel
            ) { interactionState.confirmsDeletion = false }
        }
    }

    private var amountAndFrequencyText: UiVerbatimText {
        .joined([
            .verbatim(formattedAmount),
            .message(
                category
                    .spendingFrequency == .monthly ? .extractedSpendingCategoriesManagerFrequencySelectMonthly9b11f6b7 :
                    .extractedSpendingCategoriesManagerFrequencySelectAnnually1ec9d1d5
            )
        ])
    }

    private var formattedAmount: String {
        guard let exponent = Currency.minorUnitExponent(for: category.amount.currency) else {
            return ""
        }
        return UiMessages.currency(
            category.amount.majorUnitDecimal(minorUnitExponent: exponent),
            code: category.amount.currency.uppercased(),
            locale: locale
        )
    }
}
