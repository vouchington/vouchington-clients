public enum TopHashtagMapping: String, Codable, CaseIterable, Sendable {
    case all
    case linked
    case unlinked
}

public struct TopHashtag: Codable, Identifiable, Sendable {
    public let topicAliasId: String
    public let hashtag: String
    public let itemCount: Int
    public let contributorCount: Int
    public let latestContentId: String
    public let topicId: String?

    public var id: String {
        topicAliasId
    }
}

public struct TopHashtagsResponse: Codable, Sendable {
    public let results: [TopHashtag]
    public let pageInfo: Page<TopHashtag>.PageInfo
    public let topics: [String: Topic]
}
