import Foundation

/// A decoded RSS feed source from the `/api/v1/rss-feeds` or
/// `/api/v1/users/{id}/rss-feeds/{listType}` endpoints.
///
/// Models the subset of `ViewRssFeed` fields needed for display.
/// Decoded with `keyDecodingStrategy = .convertFromSnakeCase`.
public struct RssFeedSource: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let title: String
    /// One of: "article" | "podcast" | "video" | "mixed"
    public let feedType: String
    public let rssFeedUrl: SourceUrl
    public let isDiscoverable: Bool?
    public let isEnabled: Bool?
    public let lastFetchedAt: Date?
    @RequiredNullable
    public var etag: String?
    @RequiredNullable
    public var homePageUrl: SourceUrl?
    @RequiredNullable
    public var lastModifiedAt: Date?
    @RequiredNullable
    public var hostname: SourceHostname?
    public let topic: RssFeedTopic?
    @RequiredNullable
    public var publisherType: TopicReference?
    @RequiredNullable
    public var podcastShow: PodcastShowInfo?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id
        case title
        case feedType
        case rssFeedUrl
        case isDiscoverable
        case isEnabled
        case lastFetchedAt
        case etag
        case homePageUrl
        case lastModifiedAt
        case hostname
        case topic
        case publisherType
        case podcastShow
    }

    public struct SourceUrl: Codable, Sendable {
        public let entityType: String?
        public let id: String?
        public let url: String
        public let canonicalUrlId: String?
        private let canonicalUrlIdWasPresent: Bool
        public let hostname: Hostname?
        public let pathname: String?
        public let searchParams: [String: String]?

        public init(url: String) {
            entityType = nil
            id = nil
            self.url = url
            canonicalUrlId = nil
            canonicalUrlIdWasPresent = false
            hostname = nil
            pathname = nil
            searchParams = nil
        }

        public init(from decoder: any Decoder) throws {
            let container = try decoder.container(keyedBy: RssFeedSourceUrlCodingKeys.self)
            entityType = try container.decodeIfPresent(String.self, forKey: .entityType)
            id = try container.decodeIfPresent(String.self, forKey: .id)
            url = try container.decode(String.self, forKey: .url)
            canonicalUrlIdWasPresent = container.contains(.canonicalUrlId)
            canonicalUrlId = try container.decodeIfPresent(String.self, forKey: .canonicalUrlId)
            hostname = try container.decodeIfPresent(Hostname.self, forKey: .hostname)
            pathname = try container.decodeIfPresent(String.self, forKey: .pathname)
            searchParams = try container.decodeIfPresent([String: String].self, forKey: .searchParams)
        }

        public func encode(to encoder: any Encoder) throws {
            var container = encoder.container(keyedBy: RssFeedSourceUrlCodingKeys.self)
            try container.encodeIfPresent(entityType, forKey: .entityType)
            try container.encodeIfPresent(id, forKey: .id)
            try container.encode(url, forKey: .url)
            if canonicalUrlIdWasPresent {
                try container.encode(RequiredNullable(wrappedValue: canonicalUrlId), forKey: .canonicalUrlId)
            }
            try container.encodeIfPresent(hostname, forKey: .hostname)
            try container.encodeIfPresent(pathname, forKey: .pathname)
            try container.encodeIfPresent(searchParams, forKey: .searchParams)
        }
    }

    public struct SourceHostname: Codable, Sendable {
        public let hostname: String

        public init(hostname: String) {
            self.hostname = hostname
        }
    }

    public struct PodcastShowInfo: Codable, Sendable {
        /// Proxied cover art URL; nil when no itunes:image is present.
        public let coverArtUrl: String?

        public init(coverArtUrl: String?) {
            self.coverArtUrl = coverArtUrl
        }
    }

    public init(
        id: String,
        title: String,
        feedType: String,
        rssFeedUrl: SourceUrl,
        entityType: String? = nil,
        isDiscoverable: Bool? = nil,
        isEnabled: Bool? = nil,
        lastFetchedAt: Date? = nil,
        hostname: SourceHostname?,
        topic: RssFeedTopic?,
        publisherType: TopicReference?,
        podcastShow: PodcastShowInfo?
    ) {
        self.entityType = entityType
        self.id = id
        self.title = title
        self.feedType = feedType
        self.rssFeedUrl = rssFeedUrl
        self.isDiscoverable = isDiscoverable
        self.isEnabled = isEnabled
        self.lastFetchedAt = lastFetchedAt
        etag = nil
        homePageUrl = nil
        lastModifiedAt = nil
        self.hostname = hostname
        self.topic = topic
        self.publisherType = publisherType
        self.podcastShow = podcastShow
    }

    /// Human-readable host label: hostname.displayName when available,
    /// otherwise the host component of rssFeedUrl.url.
    public var displayHost: String {
        if let host = hostname {
            return host.hostname
        }
        guard let url = URL(string: rssFeedUrl.url), let host = url.host else {
            return rssFeedUrl.url
        }
        return host
    }
}

private enum RssFeedSourceUrlCodingKeys: String, CodingKey {
    case entityType = "__entityType"
    case id, url, canonicalUrlId, hostname, pathname, searchParams
}
