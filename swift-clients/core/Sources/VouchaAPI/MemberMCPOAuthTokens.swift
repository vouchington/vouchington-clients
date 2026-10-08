import Foundation

public struct MemberMCPOAuthTokens: Codable, Sendable {
    public let accessToken: String
    public let refreshToken: String
    public let expiresIn: Int
    public let scope: String
    public let tokenType: String
    /// Persisted with the rotating tokens so an app restart retains the expiry boundary.
    public let acquiredAt: Date

    public init(
        accessToken: String,
        refreshToken: String,
        expiresIn: Int,
        scope: String,
        tokenType: String,
        acquiredAt: Date = Date()
    ) {
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        self.expiresIn = expiresIn
        self.scope = scope
        self.tokenType = tokenType
        self.acquiredAt = acquiredAt
    }

    enum CodingKeys: String, CodingKey {
        case accessToken = "access_token"
        case refreshToken = "refresh_token"
        case expiresIn = "expires_in"
        case scope
        case tokenType = "token_type"
        case acquiredAt = "acquired_at"
    }

    public init(from decoder: Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            accessToken: values.decode(String.self, forKey: .accessToken),
            refreshToken: values.decode(String.self, forKey: .refreshToken),
            expiresIn: values.decode(Int.self, forKey: .expiresIn),
            scope: values.decode(String.self, forKey: .scope),
            tokenType: values.decode(String.self, forKey: .tokenType),
            acquiredAt: values.decodeIfPresent(Date.self, forKey: .acquiredAt) ?? Date()
        )
    }
}

/// Platform adapters must persist this through Keychain (Apple) or the user's Windows credential store.
public protocol MemberMCPOAuthTokenStore: Sendable {
    func load(accountId: String) async throws -> MemberMCPOAuthTokens?
    func save(_ tokens: MemberMCPOAuthTokens, accountId: String) async throws
    func clear(accountId: String) async throws
}
