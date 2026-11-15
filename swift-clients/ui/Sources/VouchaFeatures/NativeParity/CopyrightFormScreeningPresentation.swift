import VouchaLocalization
import VouchaModels

extension CopyrightStaffFormScreening {
    /// Copy for the screening attempt a staff form review names; unknown states stay hidden
    /// instead of being guessed from a different attempt.
    var statusKey: UiMessageKey? {
        switch state {
        case "pending": .nativeSwiftAiRunStatusCopyrightScreeningPending
        case "failed": .nativeSwiftAiRunStatusCopyrightScreeningFailed
        case "completed": .nativeSwiftAiRunStatusCopyrightScreeningCompleted
        default: nil
        }
    }
}
