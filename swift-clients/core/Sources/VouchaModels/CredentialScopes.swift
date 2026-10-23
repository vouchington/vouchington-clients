import Foundation

public enum ScopeAudience: String, Codable, Sendable {
    case user, api, admin, brand, legal
    case communityModeration = "community_moderation"
}

public enum ScopeAction: String, Codable, Sendable {
    case read, write
}

public enum ScopeSurface: String, Codable, Sendable {
    case apiKey = "api-key"
    case oauth
}

public enum ScopeDescriptionKey: String, Codable, Sendable {
    case communityModerationRead = "community_moderation_read"
    case communityModerationWrite = "community_moderation_write"
    case mcpUserFullAccess = "mcp_user_full_access"
    case mcpAdminFullAccess = "mcp_admin_full_access"
    case financialProfileRead = "financial_profile_read"
    case financialProfileWrite = "financial_profile_write"
    case spendingRead = "spending_read"
    case spendingWrite = "spending_write"
}

public struct CredentialScope: Codable, Identifiable, Sendable {
    public let scope: String
    public let resource: String
    public let action: ScopeAction
    public let audience: ScopeAudience
    public let requires: String?
    public let descriptionKey: ScopeDescriptionKey?
    public let surfaces: [ScopeSurface]
    public var id: String {
        scope
    }

    public init(
        scope: String, resource: String, action: ScopeAction, audience: ScopeAudience,
        requires: String?, descriptionKey: ScopeDescriptionKey?, surfaces: [ScopeSurface]
    ) {
        self.scope = scope
        self.resource = resource
        self.action = action
        self.audience = audience
        self.requires = requires
        self.descriptionKey = descriptionKey
        self.surfaces = surfaces
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        scope = try container.decode(String.self, forKey: .scope)
        resource = try container.decode(String.self, forKey: .resource)
        action = try container.decode(ScopeAction.self, forKey: .action)
        audience = try container.decode(ScopeAudience.self, forKey: .audience)
        requires = try container.decodeIfPresent(String.self, forKey: .requires)
        let description = try container.decodeIfPresent(String.self, forKey: .descriptionKey)
        descriptionKey = description.flatMap(ScopeDescriptionKey.init(rawValue:))
        surfaces = try container.decode([ScopeSurface].self, forKey: .surfaces)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(scope, forKey: .scope)
        try container.encode(resource, forKey: .resource)
        try container.encode(action, forKey: .action)
        try container.encode(audience, forKey: .audience)
        try container.encode(requires, forKey: .requires)
        try container.encode(descriptionKey, forKey: .descriptionKey)
        try container.encode(surfaces, forKey: .surfaces)
    }

    private enum CodingKeys: String, CodingKey {
        case scope, resource, action, audience, requires, descriptionKey, surfaces
    }
}

public struct ScopeAudiencePolicy: Codable, Sendable {
    public let audience: ScopeAudience
    public let acceptsApiKeys: Bool
    public let requiresAdministrator: Bool
}

public struct ScopeCatalogResponse: Codable, Sendable {
    public let scopes: [CredentialScope]
    public let audiences: [ScopeAudiencePolicy]
}

public struct OAuthGrantClient: Codable, Sendable {
    public let id: String
    public let clientId: String
    public let clientName: String
    public let verified: Bool
}

public struct OAuthGrant: Codable, Identifiable, Sendable {
    public let id: String
    public let client: OAuthGrantClient
    public let resource: String
    public let scopes: [String]
    public let consentedAt: Date
    public let lastUsedAt: Date?
    public let attestationStatus: String?
    public let sessionId: String?

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        client = try container.decode(OAuthGrantClient.self, forKey: .client)
        resource = try container.decode(String.self, forKey: .resource)
        scopes = try container.decode([String].self, forKey: .scopes)
        consentedAt = try container.decode(Date.self, forKey: .consentedAt)
        lastUsedAt = try container.decode(Date?.self, forKey: .lastUsedAt)
        attestationStatus = try container.decode(String?.self, forKey: .attestationStatus)
        sessionId = try container.decode(String?.self, forKey: .sessionId)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(client, forKey: .client)
        try container.encode(resource, forKey: .resource)
        try container.encode(scopes, forKey: .scopes)
        try container.encode(consentedAt, forKey: .consentedAt)
        try container.encode(lastUsedAt, forKey: .lastUsedAt)
        try container.encode(attestationStatus, forKey: .attestationStatus)
        try container.encode(sessionId, forKey: .sessionId)
    }

    private enum CodingKeys: String, CodingKey {
        case id, client, resource, scopes, consentedAt, lastUsedAt, attestationStatus, sessionId
    }
}
