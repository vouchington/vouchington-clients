import Foundation
import Observation
import VouchaModels

@Observable
@MainActor
final class SpendingCategoryRowInteractionState {
    var isEditing = false
    var draft: SpendingCategoryDraft
    var confirmsDeletion = false

    init(category: SpendingCategory) {
        draft = SpendingCategoryDraft(category: category)
    }
}
