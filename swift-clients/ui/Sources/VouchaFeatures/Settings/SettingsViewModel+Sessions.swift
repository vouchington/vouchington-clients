import VouchaAPI
import VouchaLocalization
import VouchaModels

public extension SettingsViewModel {
    var sessions: [AuthSession] {
        sessionPagination.items
    }

    func revokeSession(id: String) async {
        guard let client, let session = sessions.first(where: { $0.id == id }) else { return }
        var shouldLogout = false
        await mutate {
            let _: EmptyResponse = try await client.send(.revokeAuthSession(id: id))
            reconcileRevokedSession(id: id)
            statusMessage = .message(
                session.isCurrent
                    ? .nativeSwiftSettingsCurrentSessionRevoked
                    : .nativeSwiftSettingsSessionRevoked
            )
            shouldLogout = session.isCurrent
        }
        if shouldLogout {
            onLogoutRequired()
        }
    }

    func revokeAllSessions() async {
        guard let client else { return }
        var shouldLogout = false
        await mutate {
            let _: EmptyResponse = try await client.send(.revokeAuthSessions)
            settingsLoadGeneration += 1
            revokedAllSessions = true
            sessionPagination.reset(items: [])
            sessionPagination.restoreContinuation(endCursor: nil, hasMore: false)
            statusMessage = .message(.nativeSwiftSettingsAllSessionsRevoked)
            shouldLogout = true
        }
        if shouldLogout {
            onLogoutRequired()
        }
    }
}
