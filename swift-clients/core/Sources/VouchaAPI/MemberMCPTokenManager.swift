import Foundation

/// Refresh-token rotation is single-flight per signed-in member. Save must finish before access use.
public actor MemberMCPTokenManager {
    public enum Failure: Error, Sendable { case notAuthorized }

    private let scope: MemberMCPOAuthTokenScope
    private let store: any MemberMCPOAuthTokenStore
    private let tokenClient: MemberMCPOAuthTokenClient
    private var refreshTask: Task<MemberMCPOAuthTokens, Error>?
    private var redeemTask: Task<MemberMCPOAuthTokens, Error>?
    private var latestRefreshedTokens: MemberMCPOAuthTokens?
    private var signedOut = false
    private var clearing = false

    public init(
        accountId: String,
        store: any MemberMCPOAuthTokenStore,
        tokenClient: MemberMCPOAuthTokenClient
    ) {
        scope = tokenClient.scope(for: accountId)
        self.store = store
        self.tokenClient = tokenClient
    }

    public func accessToken() async throws -> String {
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        guard let tokens = try await store.load(scope: scope) else { throw Failure.notAuthorized }
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        if tokens.acquiredAt.addingTimeInterval(TimeInterval(tokens.expiresIn - 30)) <= Date() {
            return try await refresh()
        }
        return tokens.accessToken
    }

    public func redeem(code: String, verifier: String, redirectURI: URL) async throws -> String {
        guard !signedOut, !clearing, redeemTask == nil else { throw Failure.notAuthorized }
        let previousRefresh = refreshTask
        let tokenClient = tokenClient
        let store = store
        let scope = scope
        let task = Task<MemberMCPOAuthTokens, Error> {
            if let previousRefresh { _ = try? await previousRefresh.value }
            let tokens = try await tokenClient.redeem(code: code, verifier: verifier, redirectURI: redirectURI)
            try await store.save(tokens, scope: scope)
            return tokens
        }
        redeemTask = task
        defer { redeemTask = nil }
        let tokens = try await task.value
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        latestRefreshedTokens = tokens
        return tokens.accessToken
    }

    public func signOut() async throws {
        try await beginSignOut().value
    }

    func beginSignOut() -> Task<Void, Error> {
        signedOut = true
        return Task { try await finishSignOut() }
    }

    private func finishSignOut() async throws {
        if let refreshTask { _ = try? await refreshTask.value }
        if let redeemTask { _ = try? await redeemTask.value }
        do {
            let tokens = try await store.load(scope: scope)
            if let tokens { try await tokenClient.revoke(tokens.refreshToken) }
        } catch {
            try await store.clear(scope: scope)
            throw error
        }
        try await store.clear(scope: scope)
    }

    public func clear() async throws {
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        clearing = true
        if let refreshTask { _ = try? await refreshTask.value }
        if let redeemTask { _ = try? await redeemTask.value }
        do {
            try await store.clear(scope: scope)
            latestRefreshedTokens = nil
            clearing = false
        } catch {
            clearing = false
            throw error
        }
    }

    public func refresh() async throws -> String {
        guard !signedOut, !clearing, redeemTask == nil else { throw Failure.notAuthorized }
        if let refreshTask {
            let result = try await refreshTask.value
            guard !signedOut, !clearing else { throw Failure.notAuthorized }
            return result.accessToken
        }
        let scope = scope
        let store = store
        let tokenClient = tokenClient
        let task = Task<MemberMCPOAuthTokens, Error> {
            guard let previous = try await store.load(scope: scope) else {
                throw Failure.notAuthorized
            }
            do {
                let next = try await tokenClient.refresh(previous.refreshToken)
                do {
                    try await store.save(next, scope: scope)
                } catch {
                    try? await store.clear(scope: scope)
                    throw Failure.notAuthorized
                }
                return next
            } catch MemberMCPOAuthTokenClient.Failure.invalidGrant {
                try await store.clear(scope: scope)
                throw Failure.notAuthorized
            }
        }
        refreshTask = task
        defer { refreshTask = nil }
        let result: MemberMCPOAuthTokens
        do { result = try await task.value } catch {
            latestRefreshedTokens = nil
            throw error
        }
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        latestRefreshedTokens = result
        return result.accessToken
    }

}

extension MemberMCPTokenManager {
    /// A late 401 for an older token must reuse a rotation that already succeeded.
    public func refresh(rejectedAccessToken: String) async throws -> String {
        if let newer = try await newerAccessToken(than: rejectedAccessToken) { return newer }
        let current = try await store.load(scope: scope)
        if let newer = try await newerAccessToken(than: rejectedAccessToken) { return newer }
        guard let current else { throw Failure.notAuthorized }
        if current.accessToken != rejectedAccessToken,
           current.acquiredAt.addingTimeInterval(TimeInterval(current.expiresIn - 30)) > Date() {
            return current.accessToken
        }
        return try await refresh()
    }

    private func newerAccessToken(than rejected: String) async throws -> String? {
        guard !signedOut, !clearing else { throw Failure.notAuthorized }
        if let redeemTask {
            let result = try await redeemTask.value
            guard !signedOut, !clearing else { throw Failure.notAuthorized }
            return result.accessToken
        }
        if let refreshTask {
            let result = try await refreshTask.value
            guard !signedOut, !clearing else { throw Failure.notAuthorized }
            return result.accessToken
        }
        if let latestRefreshedTokens,
           latestRefreshedTokens.accessToken != rejected,
           latestRefreshedTokens.acquiredAt.addingTimeInterval(
               TimeInterval(latestRefreshedTokens.expiresIn - 30)
           ) > Date() {
            return latestRefreshedTokens.accessToken
        }
        return nil
    }
}
