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
