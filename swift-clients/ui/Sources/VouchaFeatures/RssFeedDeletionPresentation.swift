import VouchaAPI
import VouchaLocalization

public extension RssFeedDeletionOutcome {
    var messageKey: UiMessageKey {
        switch self {
        case .accepted: .sharedRssFeedDeletionAccepted
        case .blockedByAdminImports: .sharedRssFeedDeletionBlockedByAdminImports
        }
    }
}
