import Observation

@Observable @MainActor
final class AndroidCookiePersistenceNotice {
    private(set) var errorMessage: String?
    private var revision = -1

    func accept(_ status: AndroidSessionCookieStorage.PersistenceStatus) {
        guard status.revision > revision else { return }
        revision = status.revision
        errorMessage = status.enabled ? status.errorMessage : nil
    }
}
