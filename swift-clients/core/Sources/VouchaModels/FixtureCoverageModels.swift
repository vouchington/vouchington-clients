import Foundation

public struct FixtureReference: Codable, Identifiable, Sendable {
    public let id: String
}

public struct ScoreVote: Codable, Sendable {
    public let entityType: String
    public let choice: ElectionVoteChoice
    public let createdAt: Date?
    public let entityId: String?
    public let userId: String?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case choice, createdAt, entityId, userId
    }
}

public struct Hostname: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let hostname: String
    @RequiredNullable
    public var topicId: String?
    public let blocked: Bool?
    public let crawlable: Bool?
    public let linkRelFollow: Bool?
    public let skipWebRisk: Bool?
    public let votesCountDown: Int?
    public let votesCountUp: Int?
    public let votesScoreNet: Int?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
        case hostname
        case topicId
        case blocked, crawlable, linkRelFollow, skipWebRisk, votesCountDown, votesCountUp, votesScoreNet
    }
}

public struct Url: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let hostname: Hostname?
    @RequiredNullable
    public var canonicalUrlId: String?
    public let pathname: String
    public let searchParams: [String: String]?
    public let url: String

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
        case hostname
        case canonicalUrlId
        case pathname
        case searchParams
        case url
    }
}

public struct HostnameTopUrl: Codable, Identifiable, Sendable {
    public let id: String
    public let pathname: String
    public let url: String
}

public struct NotificationsResponse: Codable, Sendable {
    public let notifications: [String: VouchaNotification]
    public let results: [NotificationResult]
    public let communities: [String: NotificationCommunity]?
    public let pageInfo: Page<FixtureReference>.PageInfo
}

public struct PostFeedResponse: Codable, Sendable {
    public let results: [PostFeedResult]
    public let pageInfo: Page<PostFeedResult>.PageInfo
    public let posts: [String: Post]
    public let postsMetrics: [String: PostMetrics]?
    public let postElections: [String: PostElection]?
    public let electionVotes: [String: ScoreVote]?
    public let bookmarks: [String: [String: Bool]]?
    public let users: [String: PublicUser]?
}

public struct PostFeedResult: Codable, Identifiable, Sendable {
    public let entityId: String
    public let postType: PostType?
    public let deliveryType: String?
    public let sharedByUserId: String?

    public var id: String {
        entityId
    }
}

public struct HostnameResponse: Codable, Sendable {
    @RequiredNullable
    public var electionVote: ScoreVote?
    public let hostname: Hostname
    public let hostnameElection: RssFeedElectionSummary?
    public let rssFeeds: [RssFeedSource]
    public let topUrls: [Url]
    @RequiredNullable
    public var topic: Topic?
}

public struct HostnamesResponse: Codable, Sendable {
    public let results: [FixtureReference]
    public let pageInfo: Page<FixtureReference>.PageInfo
    public let hostnames: [String: Hostname]
    public let topics: [String: Topic]
    public let topUrlsByHostnameId: [String: [HostnameTopUrl]]
    public let electionVotes: [String: ScoreVote]
    public let hostnameElections: [String: RssFeedElectionSummary]
}

public struct UrlResponse: Codable, Sendable {
    public let canTriggerCrawl: Bool
    public let canViewCrawlHistory: Bool
    public let canViewLatestCrawl: Bool
    @RequiredNullable
    public var latestCrawl: UrlCrawl?
    public let rssFeedId: String?
    public let url: Url
    public let urlType: String?
}

public struct UrlsResponse: Codable, Sendable {
    public let results: [Url]
    public let pageInfo: Page<Url>.PageInfo
}

public struct UrlCrawlResponse: Codable, Sendable {
    public let crawl: UrlCrawl
    public let ogImageSideload: String?
}

public struct UrlCrawlsResponse: Codable, Sendable {
    public let results: [UrlCrawl]
    public let pageInfo: Page<UrlCrawl>.PageInfo
}

public struct UrlCrawlTriggerResponse: Codable, Sendable {
    public let enqueuedCount: Int
    public let message: String
    public let rssFeedId: String?
    public let success: Bool
    public let target: String
}

public struct RssFeedItemFeedResponse: Codable, Sendable {
    public let results: [FixtureReference]
    public let pageInfo: Page<FixtureReference>.PageInfo
    public let rssFeedItems: [String: RssFeedItem]
    public let rssFeedItemElections: [String: RssFeedItemElection]
    public let electionVotes: [String: ElectionVote]
    public let storyMemberIds: [String: [String]]?
    public let storyPostIds: [String: String]?
}
