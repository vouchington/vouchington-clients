import Foundation

public struct NativeEmailTokenResponse: Codable, Equatable, Sendable {
    public let emailAddress: String
}

public struct NativeSessionTokenPayload: Codable, Equatable, Sendable {
    public let did: String
    public let sid: String?
    public let uid: String?
}

public struct NativeSessionToken: Codable, Equatable, Sendable {
    public let token: String
    public let payload: NativeSessionTokenPayload
}

public struct NativeSessionIdentity: Codable, Equatable, Sendable {
    public let did: String
    public let sid: String
    public let uid: String
}

public struct NativeLoginUser: Codable, Equatable, Sendable {
    public let entityType: String
    public let id: String
    public let emailAddress: String?
    public let accountType: AccountType?
    public let roles: [String]

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, emailAddress, accountType, roles
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(entityType, forKey: .entityType)
        try container.encode(id, forKey: .id)
        try container.encode(emailAddress, forKey: .emailAddress)
        try container.encode(accountType, forKey: .accountType)
        try container.encode(roles, forKey: .roles)
    }
}

/// Body-token result of `POST /api/v1/auth/native/email-address/login` when sign-in completes.
public struct NativeEmailLoginAuthenticatedResponse: Codable, Equatable, Sendable {
    public let did: String
    public let sid: String
    public let uid: String
    public let deviceToken: NativeSessionToken
    public let sessionToken: NativeSessionToken
    public let session: NativeSessionIdentity
    public let user: NativeLoginUser

    private enum CodingKeys: String, CodingKey {
        case did, sid, uid, session, user
        case deviceToken = "dt"
        case sessionToken = "st"
    }
}

/// Result of the same route when the account must complete multi-factor verification.
public struct NativeEmailLoginMFAResponse: Codable, Equatable, Sendable {
    public let loginAttemptId: String
    public let mfaRequired: Bool
}

/// Result of `POST /api/v1/oauth/native-session-tokens`.
public struct NativeSessionMCPTokenResponse: Codable, Equatable, Sendable {
    public let accessToken: String
    public let expiresIn: Int
    public let resource: String
    public let scope: String
    public let tokenType: String
}
