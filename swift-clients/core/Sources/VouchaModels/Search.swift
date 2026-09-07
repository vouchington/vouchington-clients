public struct OmnisearchResponse: Decodable, Sendable {
    public let topics: [OmnisearchTopic]
    public let posts: [OmnisearchPost]
    public let news: [OmnisearchNewsItem]
    public let domains: [OmnisearchDomain]
    public let communities: [OmnisearchCommunity]
}

public struct OmnisearchTopic: Decodable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let topicType: String
}

public struct OmnisearchPost: Decodable, Identifiable, Sendable {
    public let id: String
    public let postType: String
    public let title: String
    public let authoredTitle: String?
    public let declaredLanguage: String?
    public let linguaRsDetectedLanguage: String?
}

public struct OmnisearchNewsItem: Decodable, Identifiable, Sendable {
    public let id: String
    public let url: String
    public let title: String
    public let feedTitle: String
}

public struct OmnisearchDomain: Decodable, Identifiable, Sendable {
    public let id: String
    public let hostname: String
}

public struct OmnisearchCommunity: Decodable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String
    public let bookmarked: Bool
}
