import Foundation

public struct RssFeed: Codable, Identifiable, Sendable {
    public let id: String
    public let topicId: String?
    public let rssFeedUrl: String
    public let title: String?
    public let lastCrawledAt: Date?
    public let enabled: Bool
    public let discoverable: Bool
    public let publisherType: TopicReference?
}

struct EmbeddedRssFeedSidecar: Decodable {
    let entityType: String?
    let id: String
    let title: String?
    let feedType: String?
    let rssFeedUrl: RssFeedSource.SourceUrl?
    let hostname: RssFeedSource.SourceHostname?
    let topic: RssFeedTopic?
    let publisherType: TopicReference?
    let podcastShow: RssFeedSource.PodcastShowInfo?
    let isDiscoverable: Bool?
    let isEnabled: Bool?
    let lastFetchedAt: Date?

    enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, title, feedType, rssFeedUrl, hostname, topic, publisherType, podcastShow
        case isDiscoverable, isEnabled, lastFetchedAt
    }

    var resolvedSource: RssFeedSource? {
        guard title != nil || feedType != nil || rssFeedUrl != nil || podcastShow != nil else { return nil }
        return RssFeedSource(
            id: id,
            title: title ?? "",
            feedType: feedType ?? "podcast",
            rssFeedUrl: rssFeedUrl ?? .init(url: ""),
            entityType: entityType,
            isDiscoverable: isDiscoverable,
            isEnabled: isEnabled,
            lastFetchedAt: lastFetchedAt,
            hostname: hostname,
            topic: topic,
            publisherType: publisherType,
            podcastShow: podcastShow
        )
    }
}

public struct RssFeedItem: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let rssFeedId: String
    public let title: String?
    public let description: String?
    public let content: String?
    public let link: String?
    public let url: RssFeedItemURL?
    public let publishedAt: Date?
    public let creator: String?
    public let categories: [RssFeedItemCategory]?
    public let data: RssFeedItemData?
    public let rssFeedSources: [RssFeedSource]?
    public let rssFeed: RssFeedSource?
    public let mediaContent: MediaContent?
    public let thumbnailURL: String?
    public let mediaType: String?
    public let enclosureURL: String?
    public let enclosureType: String?
    public let durationSeconds: Int?
    public let videoID: String?
    public let videoPlatform: String?
    public let election: RssFeedItemElection?
    public let guid: String?

    public typealias RssFeedItemCategory = RssFeedItemCategoryModel
    public typealias MediaContent = RssFeedItemMediaContent
    public typealias RssFeedItemData = RssFeedItemDataModel
    public typealias RssFeedItemURL = RssFeedItemURLModel

    public init(
        id: String,
        rssFeedId: String,
        title: String?,
        description: String?,
        content: String?,
        link: String?,
        url: RssFeedItemURL? = nil,
        publishedAt: Date?,
        creator: String?,
        categories: [RssFeedItemCategory]?,
        data: RssFeedItemData? = nil,
        rssFeed: RssFeedSource? = nil,
        mediaContent: MediaContent?,
        thumbnailURL: String? = nil,
        mediaType: String? = nil,
        enclosureURL: String? = nil,
        enclosureType: String? = nil,
        durationSeconds: Int? = nil,
        videoID: String? = nil,
        videoPlatform: String? = nil,
        election: RssFeedItemElection? = nil
    ) {
        entityType = nil
        self.id = id
        self.rssFeedId = rssFeedId
        self.title = title
        self.description = description
        self.content = content
        self.link = link
        self.url = url
        self.publishedAt = publishedAt
        self.creator = creator
        self.categories = categories
        self.data = data
        rssFeedSources = nil
        self.rssFeed = rssFeed
        self.mediaContent = mediaContent
        self.thumbnailURL = thumbnailURL
        self.mediaType = mediaType
        self.enclosureURL = enclosureURL
        self.enclosureType = enclosureType
        self.durationSeconds = durationSeconds
        self.videoID = videoID
        self.videoPlatform = videoPlatform
        self.election = election
        guid = nil
    }

    enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
        case rssFeedId
        case title
        case description
        case content
        case link
        case url
        case publishedAt
        case creator
        case categories
        case data
        case rssFeedSources
        case rssFeed
        case mediaContent
        case thumbnailURL = "thumbnailUrl"
        case mediaType
        case enclosureURL = "enclosureUrl"
        case enclosureType
        case durationSeconds
        case videoID = "videoId"
        case videoPlatform
        case election
        case guid
    }

}
