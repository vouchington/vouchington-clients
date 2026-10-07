import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import Observation
import VouchaAPI
import VouchaCore
import VouchaModels

public enum SessionState: Sendable {
    case anonymous
    case signedIn(userId: String, username: String, membershipPlan: String?, roles: [String], accountType: AccountType?)
}

/// Manages the signed-in state by fetching the current identity from the server.
@Observable
@MainActor
public final class SessionManager {
    public private(set) var state: SessionState = .anonymous
    public private(set) var isRefreshing: Bool = false
    public private(set) var uiLocale: String?

    private let client: APIClient
    private let cookieStorage: HTTPCookieStorage
    private let logger = VouchaLogger(category: "SessionManager")

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

    public init(client: APIClient, cookieStorage: HTTPCookieStorage) {
        self.client = client
        self.cookieStorage = cookieStorage
    }

    /// Load persisted session state from cookies (call at app startup).
    public func restoreSession() async {
        await refresh()
    }

    /// Attempt to refresh the session via GET /api/v1/my/identity.
    @discardableResult
    public func refresh() async -> Bool {
        guard !isRefreshing else { return false }
        isRefreshing = true
        defer { isRefreshing = false }
        do {
            struct IdentityResponse: Decodable { let identity: PrivateUser }
            let response: IdentityResponse = try await client.send(.myIdentity)
            let identity = response.identity
            state = .signedIn(
                userId: identity.id, username: identity.username,
                membershipPlan: identity.membershipPlan,
                roles: identity.roles,
                accountType: identity.accountType
            )
            uiLocale = identity.uiLocale
            logger.info("Session restored: \(identity.username)")
            return true
        } catch VouchaError.unauthorized {
            state = .anonymous
            uiLocale = nil
            return false
        } catch {
            logger.error("Session refresh failed: \(error.localizedDescription)")
            return false
        }
    }

    public func signOut() async {
        do {
            let _: EmptyResponse = try await client.send(.logout)
        } catch {
            // ignore sign-out errors — clear locally regardless
        }
        for cookie in cookieStorage.cookies ?? [] {
            cookieStorage.deleteCookie(cookie)
        }
        state = .anonymous
        uiLocale = nil
    }

    /// Applies a user returned by an authenticated mutation without another identity request.
    public func synchronize(with user: PrivateUser) {
        state = .signedIn(
            userId: user.id,
            username: user.username,
            membershipPlan: user.membershipPlan,
            roles: user.roles,
            accountType: user.accountType
        )
        uiLocale = user.uiLocale
    }
}

/// Used to decode empty JSON body responses `{}`.
public struct EmptyResponse: Decodable {}
