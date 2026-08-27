import Foundation
import VouchaCore

public struct OAuthBrokerCapabilitiesResponse: Codable, Sendable {
    public let providers: [String]
    public let brokerCapabilities: OAuthBrokerCapabilities

    private enum CodingKeys: String, CodingKey {
        case providers
        case brokerCapabilities
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        providers = try container.decode([String].self, forKey: .providers)
        brokerCapabilities = try container.decodeIfPresent(
            OAuthBrokerCapabilities.self,
            forKey: .brokerCapabilities
        ) ?? .disabled
    }
}

public struct OAuthBrokerCapabilities: Codable, Equatable, Sendable {
    public let facebook: OAuthBrokerCapability
    // swiftlint:disable:next identifier_name
    public let x: OAuthBrokerCapability
    public let github: OAuthBrokerCapability

    static let disabled = OAuthBrokerCapabilities(
        facebook: .disabled,
        x: .disabled,
        github: .disabled
    )

    public subscript(provider: NativeOAuthProvider) -> OAuthBrokerCapability {
        switch provider {
        case .facebook: facebook
        case .x: x
        case .github: github
        }
    }
}

public struct OAuthBrokerCapability: Codable, Equatable, Sendable {
    public let version: Int
    public let modes: OAuthBrokerModes
    public let purposes: [NativeOAuthAuthorizationPurpose]

    static let disabled = OAuthBrokerCapability(
        version: 0,
        modes: OAuthBrokerModes(web: false, native: false),
        purposes: []
    )

    public func supportsNative(_ purpose: NativeOAuthAuthorizationPurpose) -> Bool {
        version == 1 && modes.native && purposes.contains(purpose)
    }
}

public struct OAuthBrokerModes: Codable, Equatable, Sendable {
    public let web: Bool
    public let native: Bool
}

public struct BeginNativeOAuthAuthorizationResponse: Codable, Sendable {
    public let flowId: String
    public let redirectUrl: String
    public let expiresAt: Date
}

public struct OAuthAuthenticatedUser: Codable, Equatable, Sendable {
    public let id: String
    public let username: String?
    public let emailAddress: String?
    public let roles: [String]
    public let profileImageId: String?

    private enum CodingKeys: String, CodingKey {
        case id
        case username
        case emailAddress
        case roles
        case profileImageId
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(username, forKey: .username)
        try container.encode(emailAddress, forKey: .emailAddress)
        try container.encode(roles, forKey: .roles)
        try container.encode(profileImageId, forKey: .profileImageId)
    }
}

public enum NativeOAuthCompletionResponse: Equatable, Sendable {
    case pending
    case authenticated(OAuthAuthenticatedUser)
    case mfaRequired(loginAttemptId: String)
    case connected(OAuthAccountInfo)
}

extension NativeOAuthCompletionResponse: Codable {
    private enum CodingKeys: String, CodingKey {
        case status
        case user
        case mfaRequired
        case loginAttemptId
        case oauthAccount
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let status = try container.decodeIfPresent(String.self, forKey: .status)
        let user = try container.decodeIfPresent(OAuthAuthenticatedUser.self, forKey: .user)
        let mfaRequired = try container.decodeIfPresent(Bool.self, forKey: .mfaRequired) == true
        let loginAttemptId = try container.decodeIfPresent(String.self, forKey: .loginAttemptId)
        let account = try container.decodeIfPresent(OAuthAccountInfo.self, forKey: .oauthAccount)
        let outcomeCount = (status == "pending" ? 1 : 0) + (user == nil ? 0 : 1) +
            (mfaRequired ? 1 : 0) + (account == nil ? 0 : 1)
        guard outcomeCount == 1 else {
            throw DecodingError.dataCorrupted(
                .init(codingPath: decoder.codingPath, debugDescription: "Invalid OAuth completion outcome")
            )
        }
        if status == "pending" {
            self = .pending
        } else if let user {
            self = .authenticated(user)
        } else if mfaRequired, let loginAttemptId, !loginAttemptId.isEmpty {
            self = .mfaRequired(loginAttemptId: loginAttemptId)
        } else if let account {
            self = .connected(account)
        } else {
            throw DecodingError.dataCorrupted(
                .init(codingPath: decoder.codingPath, debugDescription: "Incomplete OAuth completion outcome")
            )
        }
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .pending:
            try container.encode("pending", forKey: .status)
        case let .authenticated(user):
            try container.encode(user, forKey: .user)
        case let .mfaRequired(loginAttemptId):
            try container.encode(true, forKey: .mfaRequired)
            try container.encode(loginAttemptId, forKey: .loginAttemptId)
        case let .connected(account):
            try container.encode(account, forKey: .oauthAccount)
        }
    }
}
