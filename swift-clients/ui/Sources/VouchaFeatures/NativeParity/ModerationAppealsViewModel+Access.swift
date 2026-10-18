import Foundation
import VouchaModels

extension ModerationAppealsViewModel {
    func canApprove(_ appeal: ModerationAppeal) -> Bool {
        canMutate(appeal) && appeal.approvedAt == nil && appeal.sentAt == nil
            && !(drafts[appeal.id] ?? appeal.publicResponse ?? "")
            .trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    func canSend(_ appeal: ModerationAppeal) -> Bool {
        canMutate(appeal) && appeal.approvedAt != nil && appeal.sentAt == nil
            && !ambiguousDeliveryAppealIds.contains(appeal.id)
            && (drafts[appeal.id] ?? appeal.publicResponse ?? "") == (appeal.publicResponse ?? "")
    }

    func canResolve(_ appeal: ModerationAppeal, action: ModerationAppealAction) -> Bool {
        guard canMutate(appeal), appeal.sentAt != nil else { return false }
        return action != .accept || isAdministrator || appeal.userSuspensionId == nil
    }

    func canEdit(_ appeal: ModerationAppeal) -> Bool {
        canMutate(appeal) && appeal.sentAt == nil
    }

    func canMutate(_ appeal: ModerationAppeal) -> Bool {
        canAccess && appeal.status == .pending && !isMutating
    }
}
