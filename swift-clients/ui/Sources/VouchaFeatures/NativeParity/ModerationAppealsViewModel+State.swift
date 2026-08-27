import VouchaCore
import VouchaLocalization
import VouchaModels

extension ModerationAppealsViewModel {
    func replace(with appeal: ModerationAppeal) {
        drafts[appeal.id] = draftSeed(for: appeal)
        locallyEditedDraftAppealIds.remove(appeal.id)
        guard appeal.status == selectedStatus else {
            appeals.removeAll { $0.id == appeal.id }
            return
        }
        if let index = appeals.firstIndex(where: { $0.id == appeal.id }) {
            appeals[index] = appeal
        } else {
            appeals.insert(appeal, at: 0)
        }
    }

    func seedDrafts(from appeals: [ModerationAppeal]) {
        for appeal in appeals where !locallyEditedDraftAppealIds.contains(appeal.id) {
            drafts[appeal.id] = draftSeed(for: appeal)
        }
    }

    func setDraft(_ draft: String, for appeal: ModerationAppeal) {
        drafts[appeal.id] = draft
        if draft == draftSeed(for: appeal) {
            locallyEditedDraftAppealIds.remove(appeal.id)
        } else {
            locallyEditedDraftAppealIds.insert(appeal.id)
        }
    }

    func message(for error: Error) -> UiVerbatimText {
        if let error = error as? VouchaError {
            return error.errorDescription.map(UiVerbatimText.verbatim)
                ?? .message(.nativeSwiftModerationAppealsGenericError)
        }
        return .verbatim(error.localizedDescription)
    }

    private func draftSeed(for appeal: ModerationAppeal) -> String {
        appeal.publicResponse ?? appeal.aiPublicResponse ?? ""
    }
}
