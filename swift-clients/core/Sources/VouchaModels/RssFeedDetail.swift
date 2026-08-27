import Foundation

public struct RssFeedDetailResponse: Codable, Sendable {
    public let rssFeed: RssFeedDetail
    public let latestCrawl: RssFeedCrawl?
    public let canViewLatestCrawl: Bool
}

public struct RssFeedUpdateResponse: Codable, Sendable {
    public let rssFeed: RssFeedDetail
}

public struct RssFeedDetail: Codable, Identifiable, Sendable {
    public let id: String
    public let title: String?
    public let rssFeedUrl: ViewUrl
    public let homePageUrl: ViewUrl?
    public let isEnabled: Bool
    public let isDiscoverable: Bool
    public let lastFetchedAt: Date?
    public let etag: String?
    public let lastModifiedAt: Date?
    public let topicId: String?
    public let publisherType: TopicReference?
}

public struct RssFeedCrawl: Codable, Identifiable, Sendable {
    public let id: String
    public let responseCode: Int?
    public let createdAt: Date?
}

public struct RssFeedCrawlsResponse: Codable, Sendable {
    public let results: [RssFeedCrawl]
    public let pageInfo: Page<RssFeedCrawl>.PageInfo
}

public struct RssFeedCrawlResponse: Codable, Sendable {
    public let crawl: RssFeedCrawl
}

public struct ViewUrl: Codable, Sendable {
    public let url: String
}
