public struct RssFeedTopic: Codable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String?
    public let topicType: String
    public let createdBy: PublicUser?
    public let updatedBy: PublicUser?
    private let encodedFields: [String: DecodedJSONValue]

    private enum CodingKeys: String, CodingKey {
        case id, name, slug, topicType, createdBy, updatedBy
    }

    public init(
        id: String,
        name: String,
        slug: String?,
        topicType: String,
        createdBy: PublicUser? = nil,
        updatedBy: PublicUser? = nil
    ) {
        self.id = id
        self.name = name
        self.slug = slug
        self.topicType = topicType
        self.createdBy = createdBy
        self.updatedBy = updatedBy
        encodedFields = [:]
    }

    public init(from decoder: any Decoder) throws {
        let rawContainer = try decoder.container(keyedBy: RssFeedTopicCodingKey.self)
        var fields: [String: DecodedJSONValue] = [:]
        for key in rawContainer.allKeys {
            fields[key.stringValue] = try rawContainer.decode(DecodedJSONValue.self, forKey: key)
        }
        encodedFields = fields

        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        name = try container.decode(String.self, forKey: .name)
        slug = try container.decodeIfPresent(String.self, forKey: .slug)
        topicType = try container.decode(String.self, forKey: .topicType)
        createdBy = try container.decodeIfPresent(PublicUser.self, forKey: .createdBy)
        updatedBy = try container.decodeIfPresent(PublicUser.self, forKey: .updatedBy)
    }

    public func encode(to encoder: any Encoder) throws {
        if encodedFields.isEmpty {
            var container = encoder.container(keyedBy: CodingKeys.self)
            try container.encode(id, forKey: .id)
            try container.encode(name, forKey: .name)
            try container.encodeIfPresent(slug, forKey: .slug)
            try container.encode(topicType, forKey: .topicType)
            try container.encodeIfPresent(createdBy, forKey: .createdBy)
            try container.encodeIfPresent(updatedBy, forKey: .updatedBy)
            return
        }

        var container = encoder.container(keyedBy: RssFeedTopicCodingKey.self)
        for (key, value) in encodedFields {
            try container.encode(value, forKey: RssFeedTopicCodingKey(stringValue: key))
        }
    }
}

private struct RssFeedTopicCodingKey: CodingKey {
    let stringValue: String
    let intValue: Int?

    init(stringValue: String) {
        self.stringValue = stringValue
        intValue = nil
    }

    init(intValue: Int) {
        stringValue = "\(intValue)"
        self.intValue = intValue
    }
}
