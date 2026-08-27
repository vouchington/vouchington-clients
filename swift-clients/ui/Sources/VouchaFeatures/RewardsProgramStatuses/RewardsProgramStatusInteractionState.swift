import Observation
import VouchaModels

@Observable
@MainActor
final class RewardsProgramStatusRowInteractionState {
    var isEditing = false
    var draft: RewardsProgramStatusDraft
    var confirmsDeletion = false

    init(status: RewardsProgramStatus) {
        draft = RewardsProgramStatusDraft(status: status)
    }
}
