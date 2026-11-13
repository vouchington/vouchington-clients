import Foundation
import VouchaAPI
import VouchaCore
import VouchaModels

public extension SessionManager {
    internal func applySessionInvalidation(epoch: UInt64, revision: UInt64) {
        let publishedAfterClear = publishedSession.map {
            $0.epoch > epoch || ($0.epoch == epoch && $0.revision >= revision)
        } ?? false
        guard client.sessionEpoch >= epoch, !publishedAfterClear else { return }
        state = .anonymous
        uiLocale = nil
    }

    func signOut() async {
        guard !signingOut else { return }
        signingOut = true
        generation &+= 1
        let signingOutGeneration = generation
        defer { if generation == signingOutGeneration { signingOut = false } }
        client.fenceSessionRequests()
        let signingOutEpoch = client.sessionEpoch
        refreshTask = nil
        isRefreshing = false
        lastSignOutErrorMessage = nil
        if let accountId = currentUserId, let beforeSignOut {
            do {
                try await beforeSignOut(accountId)
            } catch {
                logger.error("Device grant cleanup failed before sign-out: \(error.localizedDescription)")
                lastSignOutErrorMessage = error.localizedDescription
                return
            }
        }
        do {
            try await beforeClearSessionCookies?()
        } catch {
            logger.error("Cookie cleanup failed before sign-out: \(error.localizedDescription)")
            lastSignOutErrorMessage = error.localizedDescription
            return
        }
        let prepared: SessionSignOutRequest
        do {
            guard generation == signingOutGeneration else { return }
            prepared = try await client.prepareSignOut(
                expectedEpoch: signingOutEpoch,
                afterClear: clearPersistedCookies
            )
        } catch {
            if generation == signingOutGeneration { lastSignOutErrorMessage = error.localizedDescription }
            return
        }
        applyPreparedSignOut(prepared, generation: signingOutGeneration)
        try? await client.finishSignOut(prepared)
    }

    private func applyPreparedSignOut(_ prepared: SessionSignOutRequest, generation: UInt64) {
        guard self.generation == generation else { return }
        lastSignOutErrorMessage = prepared.persistenceError?.localizedDescription
        state = .anonymous
        uiLocale = nil
    }

    /// Applies a user returned by an authenticated mutation without another identity request.
    func synchronize(with user: PrivateUser) {
        signingOut = false
        generation &+= 1
        refreshTask = nil
        isRefreshing = false
        // The identity generation fences publication without discarding a pending credential rotation.
        state = .signedIn(
            userId: user.id,
            username: user.username,
            membershipPlan: user.membershipPlan,
            roles: user.roles,
            accountType: user.accountType
        )
        publishedSession = (client.sessionEpoch, client.sessionRevision)
        uiLocale = user.uiLocale
    }
}
