import SwiftUI
import VouchaLocalization
import VouchaModels

struct PointValuationRow: View {
    @Environment(\.locale)
    private var locale
    let valuation: PointValuation
    let viewModel: PointValuationsViewModel
    @State
    private var interactionState: PointValuationRowInteractionState

    init(
        valuation: PointValuation,
        viewModel: PointValuationsViewModel,
        interactionState: PointValuationRowInteractionState? = nil
    ) {
        self.valuation = valuation
        self.viewModel = viewModel
        _interactionState = State(
            initialValue: interactionState ?? PointValuationRowInteractionState(valuation: valuation)
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(verbatim: UiMessages.string(.userContent(valuation.rewardsProgram.name), locale: locale))
                .font(.headline)
            Text(
                UiMessages.string(
                    .extractedPointValuationsManagerValuationSummaryValuePerPoint7111fb3c,
                    parameters: [
                        "value": UiMessages.currency(
                            valuation.valuePerPoint.majorUnitDecimal,
                            code: valuation.valuePerPoint.currency.uppercased(),
                            maximumFractionDigits: ScaledMoney.scale,
                            locale: locale
                        )
                    ],
                    locale: locale
                )
            )
            if let note = valuation.note {
                Text(verbatim: UiMessages.string(.userContent(note), locale: locale))
            }
            if interactionState.isEditing {
                PointValuationEditor(
                    draft: $interactionState.draft,
                    isSaving: viewModel.mutatingIds.contains(valuation.id),
                    save: {
                        Task {
                            if await viewModel.save(valuation: valuation, draft: interactionState.draft) {
                                interactionState.isEditing = false
                            }
                        }
                    },
                    cancel: {
                        interactionState.draft = PointValuationDraft(valuation: valuation, locale: locale)
                        interactionState.isEditing = false
                    }
                )
            } else {
                HStack {
                    Button(
                        UiMessages.string(
                            .extractedPointValuationsManagerValuationSummaryEdit464c4ffd,
                            locale: locale
                        )
                    ) {
                        interactionState.draft = PointValuationDraft(valuation: valuation, locale: locale)
                        interactionState.isEditing = true
                    }
                    Button(
                        UiMessages.string(
                            .extractedPointValuationsManagerValuationSummaryRemoveC3812fc4,
                            locale: locale
                        ),
                        role: .destructive
                    ) { interactionState.confirmsDeletion = true }
                        .disabled(viewModel.mutatingIds.contains(valuation.id))
                }
            }
        }
        .padding(.vertical, 8)
        .confirmationDialog(
            UiMessages.string(
                .extractedPointValuationsManagerValuationSummaryRemove9fe2f243,
                locale: locale
            ),
            isPresented: $interactionState.confirmsDeletion
        ) {
            Button(
                UiMessages.string(
                    .extractedPointValuationsManagerValuationSummaryConfirmEebdd24a,
                    locale: locale
                ),
                role: .destructive
            ) { Task { _ = await viewModel.delete(valuation) } }
            Button(
                UiMessages.string(
                    .extractedPointValuationsManagerValuationSummaryCancel19766ed6,
                    locale: locale
                ),
                role: .cancel
            ) { interactionState.confirmsDeletion = false }
        }
    }
}
