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

public struct ScopeCatalogAudience: Codable, Sendable {
    public let audience: String
    public let acceptsApiKeys: Bool
    public let requiresAdministrator: Bool

    public init(audience: String, acceptsApiKeys: Bool, requiresAdministrator: Bool) {
        self.audience = audience
        self.acceptsApiKeys = acceptsApiKeys
        self.requiresAdministrator = requiresAdministrator
    }

    private enum CodingKeys: String, CodingKey {
        case audience
        case acceptsApiKeys
        case requiresAdministrator
    }
}

public struct ScopeCatalogResponse: Codable, Sendable {
    public let audiences: [ScopeCatalogAudience]
    public let scopes: [CredentialScope]

    public init(audiences: [ScopeCatalogAudience] = [], scopes: [CredentialScope]) {
        self.audiences = audiences
        self.scopes = scopes
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        audiences = try container.decodeIfPresent([ScopeCatalogAudience].self, forKey: .audiences) ?? []
        scopes = try container.decode([CredentialScope].self, forKey: .scopes)
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(audiences, forKey: .audiences)
        try container.encode(scopes, forKey: .scopes)
    }

    private enum CodingKeys: String, CodingKey {
        case audiences, scopes
    }
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

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(client, forKey: .client)
        try container.encode(resource, forKey: .resource)
        try container.encode(scopes, forKey: .scopes)
        try container.encode(consentedAt, forKey: .consentedAt)
        try container.encode(lastUsedAt, forKey: .lastUsedAt)
    }

    private enum CodingKeys: String, CodingKey {
        case id, client, resource, scopes, consentedAt, lastUsedAt
    }
}
