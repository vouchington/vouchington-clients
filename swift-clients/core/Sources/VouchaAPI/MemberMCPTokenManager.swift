import Foundation

/// Refresh-token rotation is single-flight per signed-in member. Save must finish before access use.
public actor MemberMCPTokenManager {
    public enum Failure: Error, Sendable { case notAuthorized }

    private let accountId: String
    private let store: any MemberMCPOAuthTokenStore
    private let tokenClient: MemberMCPOAuthTokenClient
    private var refreshTask: Task<MemberMCPOAuthTokens, Error>?
    private var redeemTask: Task<MemberMCPOAuthTokens, Error>?
    private var latestRefreshedTokens: MemberMCPOAuthTokens?
    private var signedOut = false

    public init(
        accountId: String,
        store: any MemberMCPOAuthTokenStore,
        tokenClient: MemberMCPOAuthTokenClient
    ) {
        self.accountId = accountId
        self.store = store
        self.tokenClient = tokenClient
    }

    public func accessToken() async throws -> String {
        guard !signedOut else { throw Failure.notAuthorized }
        guard let tokens = try await store.load(accountId: accountId) else { throw Failure.notAuthorized }
        guard !signedOut else { throw Failure.notAuthorized }
        if tokens.acquiredAt.addingTimeInterval(TimeInterval(tokens.expiresIn - 30)) <= Date() {
            return try await refresh()
        }
        return tokens.accessToken
    }

    public func redeem(code: String, verifier: String, redirectURI: URL) async throws -> String {
        guard !signedOut, redeemTask == nil else { throw Failure.notAuthorized }
        let tokenClient = tokenClient
        let store = store
        let accountId = accountId
        let task = Task<MemberMCPOAuthTokens, Error> {
            let tokens = try await tokenClient.redeem(code: code, verifier: verifier, redirectURI: redirectURI)
            try await store.save(tokens, accountId: accountId)
            return tokens
        }
        redeemTask = task
        defer { redeemTask = nil }
        let tokens = try await task.value
        guard !signedOut else { throw Failure.notAuthorized }
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
        let tokens = try await store.load(accountId: accountId)
        do {
            if let tokens { try await tokenClient.revoke(tokens.refreshToken) }
        } catch {
            try await store.clear(accountId: accountId)
            throw error
        }
        try await store.clear(accountId: accountId)
    }

    public func clear() async throws {
        signedOut = true
        if let refreshTask { _ = try? await refreshTask.value }
        if let redeemTask { _ = try? await redeemTask.value }
        try await store.clear(accountId: accountId)
    }

    public func refresh() async throws -> String {
        guard !signedOut else { throw Failure.notAuthorized }
        if let refreshTask {
            let result = try await refreshTask.value
            guard !signedOut else { throw Failure.notAuthorized }
            return result.accessToken
        }
        let accountId = accountId
        let store = store
        let tokenClient = tokenClient
        let task = Task<MemberMCPOAuthTokens, Error> {
            guard let previous = try await store.load(accountId: accountId) else {
                throw Failure.notAuthorized
            }
            do {
                let next = try await tokenClient.refresh(previous.refreshToken)
                do {
                    try await store.save(next, accountId: accountId)
                } catch {
                    try? await store.clear(accountId: accountId)
                    throw Failure.notAuthorized
                }
                return next
            } catch MemberMCPOAuthTokenClient.Failure.invalidGrant {
                try await store.clear(accountId: accountId)
                throw Failure.notAuthorized
            }
        }
        refreshTask = task
        defer { refreshTask = nil }
        let result = try await task.value
        guard !signedOut else { throw Failure.notAuthorized }
        latestRefreshedTokens = result
        return result.accessToken
    }

    /// A late 401 for an older token must reuse a rotation that already succeeded.
    public func refresh(rejectedAccessToken: String) async throws -> String {
        guard !signedOut else { throw Failure.notAuthorized }
        if let refreshTask {
            let result = try await refreshTask.value
            guard !signedOut else { throw Failure.notAuthorized }
            return result.accessToken
        }
        if let latestRefreshedTokens,
           latestRefreshedTokens.accessToken != rejectedAccessToken,
           latestRefreshedTokens.acquiredAt.addingTimeInterval(
               TimeInterval(latestRefreshedTokens.expiresIn - 30)
           ) > Date() {
            return latestRefreshedTokens.accessToken
        }
        guard let current = try await store.load(accountId: accountId) else {
            throw Failure.notAuthorized
        }
        guard !signedOut else { throw Failure.notAuthorized }
        if let refreshTask {
            let result = try await refreshTask.value
            guard !signedOut else { throw Failure.notAuthorized }
            return result.accessToken
        }
        if let latestRefreshedTokens,
           latestRefreshedTokens.accessToken != rejectedAccessToken,
           latestRefreshedTokens.acquiredAt.addingTimeInterval(
               TimeInterval(latestRefreshedTokens.expiresIn - 30)
           ) > Date() {
            return latestRefreshedTokens.accessToken
        }
        if current.accessToken != rejectedAccessToken,
           current.acquiredAt.addingTimeInterval(TimeInterval(current.expiresIn - 30)) > Date() {
            return current.accessToken
        }
        return try await refresh()
    }
}
