import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

public enum SessionState: Sendable {
    case anonymous
    case signedIn(
        userId: String,
        username: String?,
        membershipPlan: String?,
        roles: [String],
        accountType: AccountType?
    )
}

/// Manages the signed-in state by fetching the current identity from the server.
@Observable
@MainActor
public final class SessionManager {
    public internal(set) var state: SessionState = .anonymous
    public internal(set) var isRefreshing: Bool = false
    public internal(set) var uiLocale: String?
    public internal(set) var lastSignOutErrorMessage: String?
    public internal(set) var lastRefreshErrorMessage: String?

    let client: APIClient
    let beforeClearSessionCookies: (@Sendable () async throws -> Void)?
    private let beforePublishSignedIn: (@Sendable () async throws -> Void)?
    let clearPersistedCookies: (@Sendable () -> Void)?
    let logger = VouchaLogger(category: "SessionManager")
    var signingOut = false
    var refreshTask: Task<Bool, Never>?
    var generation: UInt64 = 0
    var invalidationObservation: SessionInvalidationObservation?
    var publishedSession: (epoch: UInt64, revision: UInt64)?
    var beforeSignOut: (@MainActor (String) async throws -> Void)?

    public var isSignedIn: Bool {
        if case .signedIn = state {
            return true
        }
        return false
    }

    public var currentUserRoles: [String] {
        if case let .signedIn(_, _, _, roles, _) = state {
            return roles
        }
        return []
    }

    public var currentUserAccountType: AccountType? {
        if case let .signedIn(_, _, _, _, accountType) = state {
            return accountType
        }
        return nil
    }

    public var canCastPublicVotes: Bool {
        isSignedIn && currentUserAccountType == nil
    }

    /// The signed-in user's ID, or `nil` when anonymous.
    public var currentUserId: String? {
        if case let .signedIn(userId, _, _, _, _) = state {
            return userId
        }
        return nil
    }

    public var currentMembershipPlan: String? {
        if case let .signedIn(_, _, membershipPlan, _, _) = state {
            return membershipPlan
        }
        return nil
    }

    public init(
        client: APIClient,
        clearPersistedCookies: (@Sendable () -> Void)? = nil,
        beforeClearSessionCookies: (@Sendable () async throws -> Void)? = nil,
        beforePublishSignedIn: (@Sendable () async throws -> Void)? = nil
    ) {
        self.client = client
        self.clearPersistedCookies = clearPersistedCookies
        self.beforeClearSessionCookies = beforeClearSessionCookies
        self.beforePublishSignedIn = beforePublishSignedIn
        invalidationObservation = client.observeSessionInvalidation { [weak self] epoch, revision in
            Task { @MainActor [weak self] in self?.applySessionInvalidation(epoch: epoch, revision: revision) }
        }
    }

    /// Install device-grant cleanup that must finish before the member identity is discarded.
    public func setBeforeSignOut(_ action: @escaping @MainActor (String) async throws -> Void) {
        beforeSignOut = action
    }

    /// Load persisted session state from cookies (call at app startup).
    public func restoreSession() async {
        await refresh()
    }

    /// Renew first-party credentials, then resolve the current identity.
    @discardableResult
    public func refresh() async -> Bool {
        guard !signingOut else { return false }
        if let refreshTask { return await refreshTask.value }
        let generation = generation
        let task = Task { await self.performRefresh(generation: generation) }
        refreshTask = task
        let result = await task.value
        if self.generation == generation { refreshTask = nil }
        return result
    }

    private func performRefresh(generation: UInt64) async -> Bool {
        lastRefreshErrorMessage = nil
        isRefreshing = true
        defer { if generation == self.generation { isRefreshing = false } }
        let epoch = client.sessionEpoch
        do {
            try await client.refreshSession()
            struct IdentityResponse: Decodable { let identity: PrivateUser }
            let response: IdentityResponse = try await client.send(.myIdentity)
            guard generation == self.generation, epoch == client.sessionEpoch else { return false }
            let identity = response.identity
            guard try await confirmPublication(identity, generation: generation, epoch: epoch) else { return false }
            state = .signedIn(
                userId: identity.id, username: identity.username,
                membershipPlan: identity.membershipPlan,
                roles: identity.roles,
                accountType: identity.accountType
            )
            publishedSession = (client.sessionEpoch, client.sessionRevision)
            uiLocale = identity.uiLocale
            logger.info("Session restored: \(identity.username ?? "")")
            return true
        } catch VouchaError.unauthorized {
            guard generation == self.generation else { return false }
            state = .anonymous
            uiLocale = nil
            return false
        } catch {
            guard generation == self.generation else { return false }
            if epoch != client.sessionEpoch { state = .anonymous
                uiLocale = nil
            }
            lastRefreshErrorMessage = error.localizedDescription
            logger.error("Session refresh failed: \(error.localizedDescription)")
            return false
        }
    }

    private func confirmPublication(_ identity: PrivateUser, generation: UInt64, epoch: UInt64) async throws -> Bool {
        do {
            try await beforePublishSignedIn?()
        } catch {
            guard generation == self.generation, epoch == client.sessionEpoch else { return false }
            if let currentUserId, currentUserId != identity.id {
                state = .anonymous
                uiLocale = nil
            }
            throw error
        }
        return generation == self.generation && epoch == client.sessionEpoch
    }

}

/// Used to decode empty JSON body responses `{}`.
public struct EmptyResponse: Decodable {}
