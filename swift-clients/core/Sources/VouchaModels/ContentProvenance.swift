/// Public facts about content created through an API or MCP credential.
public struct PublicContentProvenance: Codable, Sendable, Equatable {
    public let via: String
    public let app: PublicProvenanceApp?

    private enum CodingKeys: String, CodingKey { case via, app }

    public init(via: String, app: PublicProvenanceApp?) {
        self.via = via
        self.app = app
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        via = try container.decode(String.self, forKey: .via)
        app = try container.decode(RequiredNullable<PublicProvenanceApp>.self, forKey: .app).wrappedValue
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(via, forKey: .via)
        try container.encode(RequiredNullable(wrappedValue: app), forKey: .app)
    }
}

/// An app name is usable only when the producer marks its source as trusted.
public struct PublicProvenanceApp: Codable, Sendable, Equatable {
    public let kind: String
    public let key: String?
    public let hostname: String?
    public let clientId: String?
    public let clientName: String?

    public init(
        kind: String,
        key: String? = nil,
        hostname: String? = nil,
        clientId: String? = nil,
        clientName: String? = nil
    ) {
        self.kind = kind
        self.key = key
        self.hostname = hostname
        self.clientId = clientId
        self.clientName = clientName
    }
}

/// Raw creation facts included only in responses authorized for moderation staff.
public struct StaffContentProvenance: Codable, Sendable, Hashable {
    public let createdVia: String
    public let oauthClient: StaffProvenanceOAuthClient?

    public init(createdVia: String, oauthClient: StaffProvenanceOAuthClient? = nil) {
        self.createdVia = createdVia
        self.oauthClient = oauthClient
    }

    private enum CodingKeys: String, CodingKey { case createdVia, oauthClient }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        createdVia = try container.decode(String.self, forKey: .createdVia)
        oauthClient = try container.decode(
            RequiredNullable<StaffProvenanceOAuthClient>.self, forKey: .oauthClient
        ).wrappedValue
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(createdVia, forKey: .createdVia)
        try container.encode(RequiredNullable(wrappedValue: oauthClient), forKey: .oauthClient)
    }

    var rawValue: DecodedJSONValue {
        var facts: [String: DecodedJSONValue] = [
            "created_via": .string(createdVia), "oauth_client": .null
        ]
        if let oauthClient {
            facts["oauth_client"] = .object([
                "client_id": .string(oauthClient.clientId),
                "client_name": .string(oauthClient.clientName),
                "metadata_url": oauthClient.metadataUrl.map(DecodedJSONValue.string) ?? .null,
                "verified": .bool(oauthClient.verified)
            ])
        }
        return .object(facts)
    }
}

public struct StaffProvenanceOAuthClient: Codable, Sendable, Hashable {
    public let clientId: String
    public let clientName: String
    public let metadataUrl: String?
    public let verified: Bool

    public init(clientId: String, clientName: String, metadataUrl: String?, verified: Bool) {
        self.clientId = clientId
        self.clientName = clientName
        self.metadataUrl = metadataUrl
        self.verified = verified
    }

    private enum CodingKeys: String, CodingKey { case clientId, clientName, metadataUrl, verified }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        clientId = try container.decode(String.self, forKey: .clientId)
        clientName = try container.decode(String.self, forKey: .clientName)
        metadataUrl = try container.decode(RequiredNullable<String>.self, forKey: .metadataUrl).wrappedValue
        verified = try container.decode(Bool.self, forKey: .verified)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(clientId, forKey: .clientId)
        try container.encode(clientName, forKey: .clientName)
        try container.encode(RequiredNullable(wrappedValue: metadataUrl), forKey: .metadataUrl)
        try container.encode(verified, forKey: .verified)
    }
}
