import Foundation

public extension NativeOAuthAuthorizationStore {
    func isCallbackURL(_ url: URL) -> Bool {
        url.scheme?.lowercased() == "voucha" &&
            url.host?.lowercased() == "auth" &&
            url.path == "/oauth/callback"
    }

    func claimCallback(
        for url: URL,
        now: Date = Date()
    ) throws -> PendingNativeOAuthAuthorization? {
        try Self.lock.withLock {
            guard isCallbackURL(url),
                  case let .active(pending) = try callbackPendingStatusUnlocked(now: now),
                  !pending.isFinalizing,
                  let components = URLComponents(url: url, resolvingAgainstBaseURL: false),
                  components.queryItems?.first(where: { $0.name == "flow_id" })?.value == pending.flowId,
                  let token = components.queryItems?.first(where: { $0.name == "completion_token" })?.value,
                  !token.isEmpty
            else { return nil }
            let claimed = PendingNativeOAuthAuthorization(
                flowId: pending.flowId,
                provider: pending.provider,
                purpose: pending.purpose,
                completionProofVerifier: pending.completionProofVerifier,
                expiresAt: pending.expiresAt,
                completionToken: token
            )
            let state = StoredState(pending: claimed, result: nil)
            try secureState.writeForOAuthCallbackClaim(JSONEncoder().encode(state))
            return claimed
        }
    }
}

private extension NativeOAuthAuthorizationStore {
    func callbackPendingStatusUnlocked(now: Date) throws -> PendingNativeOAuthAuthorizationStatus {
        guard let data = try secureState.read() else {
            return .none
        }
        guard let stored = try? JSONDecoder().decode(StoredState.self, from: data),
              let pending = stored.pending
        else {
            try secureState.delete()
            return .none
        }
        guard now < pending.expiresAt else {
            return .expired(pending)
        }
        return .active(pending)
    }
}
