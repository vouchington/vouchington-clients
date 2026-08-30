public struct PostExplicitCategory: Codable, Equatable, Sendable {
    public let type: String
    public let topicId: String?
    public let topicName: String?
    public let hashtag: String?

    public init(
        type: String,
        topicId: String? = nil,
        topicName: String? = nil,
        hashtag: String? = nil
    ) {
        self.type = type
        self.topicId = topicId
        self.topicName = topicName
        self.hashtag = hashtag
    }

    private enum CodingKeys: String, CodingKey {
        case type
        case topicId
        case topicName
        case hashtag
    }
}

public struct PostHashtag: Codable, Equatable, Sendable {
    public let id: String
    public let key: String
    public let displayToken: String
    public let topicId: String?

    public init(id: String, key: String, displayToken: String, topicId: String? = nil) {
        self.id = id
        self.key = key
        self.displayToken = displayToken
        self.topicId = topicId
    }

    private enum CodingKeys: String, CodingKey {
        case id, key, displayToken, topicId
    }
}
