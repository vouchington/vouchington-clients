import Foundation
import Observation
import VouchaModels

@Observable
@MainActor
final class PaymentCardRowEditorState {
    var isEditing = false
    var draft: PaymentCardDraft

    init(card: PaymentCard) {
        draft = PaymentCardDraft(card: card)
    }
}
