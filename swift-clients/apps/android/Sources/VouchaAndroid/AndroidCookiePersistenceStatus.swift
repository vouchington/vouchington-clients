import Foundation

extension AndroidSessionCookieStorage {
    struct PersistenceStatus {
        let revision: Int
        let errorMessage: String?
        let enabled: Bool
        let restorationBlocked: Bool
        let unreadableSnapshotCanBeDiscarded: Bool

        init(
            revision: Int,
            errorMessage: String?,
            enabled: Bool,
            restorationBlocked: Bool = false,
            unreadableSnapshotCanBeDiscarded: Bool = false
        ) {
            self.revision = revision
            self.errorMessage = errorMessage
            self.enabled = enabled
            self.restorationBlocked = restorationBlocked
            self.unreadableSnapshotCanBeDiscarded = unreadableSnapshotCanBeDiscarded
        }
    }

}
