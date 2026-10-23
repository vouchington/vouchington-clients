public struct NativeEmailTokenResponse: Codable, Sendable {
    public let emailAddress: String
}

public struct NativeEmailMFAResponse: Codable, Sendable {
    public let loginAttemptId: String
    public let mfaRequired: Bool

    private enum CodingKeys: String, CodingKey {
        case loginAttemptId, mfaRequired
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        loginAttemptId = try container.decode(String.self, forKey: .loginAttemptId)
        mfaRequired = try container.decode(Bool.self, forKey: .mfaRequired)
        guard mfaRequired else {
            throw DecodingError.dataCorruptedError(
                forKey: .mfaRequired,
                in: container,
                debugDescription: "Expected MFA required"
            )
        }
    }
}

public struct NativeDevicePayload: Codable, Sendable {
    public let did: String
}

public struct NativeSessionPayload: Codable, Sendable {
    public let did: String
    public let sid: String
    public let uid: String
}

public struct NativeDeviceToken: Codable, Sendable {
    public let payload: NativeDevicePayload
    public let token: String
}

public struct NativeSessionToken: Codable, Sendable {
    public let payload: NativeSessionPayload
    public let token: String
}

public enum NativeAuthenticatedEntityType: String, Codable, Sendable {
    case user
}

public struct NativeEmailAuthenticatedUser: Codable, Sendable {
    public let id: String
    public let entityType: NativeAuthenticatedEntityType
    public let accountType: String?
    public let emailAddress: String?
    public let roles: [String]?

    private enum CodingKeys: String, CodingKey {
        case id, accountType, emailAddress, roles
        case entityType = "__entityType"
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(entityType, forKey: .entityType)
        try container.encode(accountType, forKey: .accountType)
        try container.encodeIfPresent(emailAddress, forKey: .emailAddress)
        try container.encodeIfPresent(roles, forKey: .roles)
    }
}

public struct NativeEmailAuthenticatedResponse: Codable, Sendable {
    public let did: String
    public let deviceToken: NativeDeviceToken
    public let session: NativeSessionPayload
    public let sid: String
    public let sessionToken: NativeSessionToken
    public let uid: String
    public let user: NativeEmailAuthenticatedUser

    private enum CodingKeys: String, CodingKey {
        case did, session, sid, uid, user
        case deviceToken = "dt"
        case sessionToken = "st"
    }
}

public enum NativeSessionTokenType: String, Codable, Sendable {
    case bearer = "Bearer"
}

public struct NativeSessionMCPTokenResponse: Codable, Sendable {
    public let accessToken: String
    public let expiresIn: Double
    public let resource: String
    public let scope: String
    public let tokenType: NativeSessionTokenType
}
