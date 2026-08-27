import Foundation
import Observation
import VouchaModels

@Observable
@MainActor
final class PointValuationRowInteractionState {
    var isEditing = false
    var draft: PointValuationDraft
    var confirmsDeletion = false

    init(valuation: PointValuation) {
        draft = PointValuationDraft(valuation: valuation)
    }
}

@Observable
@MainActor
final class PointValuationsSurfaceInteractionState {
    var createDraft: PointValuationDraft

    init(locale: Locale = .current) {
        createDraft = PointValuationDraft(locale: locale)
    }

    func updateCreateDraftLocaleIfValueEmpty(_ locale: Locale) {
        guard createDraft.valuePerPointText.isEmpty else { return }
        createDraft = PointValuationDraft(note: createDraft.note, locale: locale)
    }
}
