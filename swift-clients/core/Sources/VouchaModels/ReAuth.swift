import Foundation

/// Fresh-confirmation methods available to the signed-in account.
public struct ReAuthMethodsResponse: Codable, Equatable, Sendable {
    public let hasEmail: Bool
    public let hasPasskey: Bool
    public let hasTotp: Bool
    public let providers: [String]
}

/// One-use proof returned by every successful re-auth verification route.
public struct ReAuthTokenResponse: Codable, Equatable, Sendable {
    public let reAuthToken: String
}

public struct ReAuthPasskeyCredential: Codable, Equatable, Sendable {
    public let id: String
    public let type: String
    public let transports: [String]?
}

public struct ReAuthPasskeyOptions: Codable, Equatable, Sendable {
    public let challenge: String
    public let rpId: String
    public let timeout: Int?
    public let userVerification: String?
    public let allowCredentials: [ReAuthPasskeyCredential]?
}

public struct ReAuthPasskeyOptionsResponse: Codable, Equatable, Sendable {
    public let flowId: String
    public let options: ReAuthPasskeyOptions
}

/// Server challenge for an SDK provider (Apple or Google); the assertion must carry the nonce.
public struct ReAuthProviderChallengeResponse: Codable, Equatable, Sendable {
    public let flowId: String
    public let nonce: String
    public let expiresAt: Date
}

public struct NativeRefreshedSessionClaims: Codable, Equatable, Sendable {
    public let client: String
    public let did: String
    public let rca: Int
    public let rol: [String]
    public let sca: Int
    public let sid: String
    public let uid: String?
}

public struct NativeRefreshedSession: Codable, Equatable, Sendable {
    public let did: String
    public let deviceToken: String
    public let deviceExpiration: Int
    public let secure: Bool
    public let session: NativeRefreshedSessionClaims
    public let sid: String
    public let sessionToken: String
    public let sessionExpiration: Int
    public let uid: String?

    private enum CodingKeys: String, CodingKey {
        case did, secure, session, sid, uid
        case deviceToken = "dt"
        case deviceExpiration = "dte"
        case sessionToken = "st"
        case sessionExpiration = "ste"
    }
}

/// Result of rotating `PATCH /api/v1/session`: both replacement tokens and their lifetimes.
public struct NativeSessionRefreshResponse: Codable, Equatable, Sendable {
    public let session: NativeRefreshedSession
}

/// 403 body of a mutation that needs a bot challenge before it can proceed.
public struct BotChallengeRequiredResponse: Codable, Equatable, Sendable {
    public static let requiredCode = "BOT_CHALLENGE_REQUIRED"

    public let action: String
    public let code: String
    public let message: String
}
