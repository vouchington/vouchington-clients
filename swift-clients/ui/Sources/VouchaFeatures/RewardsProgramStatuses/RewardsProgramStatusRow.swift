import SwiftUI
import VouchaLocalization
import VouchaModels

struct RewardsProgramStatusRow: View {
    @Environment(\.locale)
    var locale
    @Environment(\.timeZone)
    var timeZone
    let status: RewardsProgramStatus
    let viewModel: RewardsProgramStatusesViewModel
    @State
    var interactionState: RewardsProgramStatusRowInteractionState

    init(
        status: RewardsProgramStatus,
        viewModel: RewardsProgramStatusesViewModel,
        interactionState: RewardsProgramStatusRowInteractionState? = nil
    ) {
        self.status = status
        self.viewModel = viewModel
        _interactionState = State(
            initialValue: interactionState ?? RewardsProgramStatusRowInteractionState(status: status)
        )
    }

}

private extension RewardsProgramStatusRow {
    var content: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(verbatim: UiMessages.string(.userContent(status.rewardsProgramStatus.name), locale: locale))
                .font(.headline)
            if interactionState.isEditing {
                dateEditor
            } else {
                dateSummary
                HStack {
                    Button(
                        UiMessages.string(
                            .extractedRewardsProgramStatusesManagerStatusSummaryEdit464c4ffd,
                            locale: locale
                        )
                    ) {
                        interactionState.draft = RewardsProgramStatusDraft(status: status)
                        interactionState.isEditing = true
                    }
                    Button(
                        UiMessages.string(
                            .extractedRewardsProgramStatusesManagerStatusSummaryRemoveC3812fc4,
                            locale: locale
                        ),
                        role: .destructive
                    ) {
                        interactionState.confirmsDeletion = true
                    }.disabled(viewModel.mutatingIds.contains(status.id))
                }
            }
        }
        .padding(.vertical, 8)
    }

}

extension RewardsProgramStatusRow {
    var body: some View {
        content.confirmationDialog(
            UiMessages.string(
                .extractedRewardsProgramStatusesManagerStatusSummaryRemove9fe2f243,
                locale: locale
            ),
            isPresented: $interactionState.confirmsDeletion
        ) {
            Button(
                UiMessages.string(
                    .extractedRewardsProgramStatusesManagerStatusSummaryConfirmEebdd24a,
                    locale: locale
                ),
                role: .destructive
            ) {
                Task { _ = await viewModel.delete(status) }
            }
            Button(
                UiMessages.string(
                    .extractedRewardsProgramStatusesManagerStatusSummaryCancel19766ed6,
                    locale: locale
                ),
                role: .cancel
            ) {
                interactionState.confirmsDeletion = false
            }
        }
    }
}
