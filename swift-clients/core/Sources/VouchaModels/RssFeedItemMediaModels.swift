public struct RssFeedItemCategoryModel: Codable, Sendable {
    public let id: String?
    public let categoryText: String
    @RequiredNullable
    public var topic: TopicReference?
    public let votesScoreNet: Double?
    @TolerantNullable
    public var hashtag: RssFeedItemHashtag?
}

public struct RssFeedItemHashtag: Codable, Sendable {
    public let id: String
    public let key: String
    public let displayToken: String
    @RequiredNullable
    public var topicId: String?
}

public struct RssFeedItemMediaContent: Codable, Sendable {
    public let url: String?
    public let type: String?
    public let medium: String?
    public let duration: Int?

    public init(url: String?, type: String?, medium: String?, duration: Int?) {
        self.url = url
        self.type = type
        self.medium = medium
        self.duration = duration
    }
}

public struct RssFeedItemDataModel: Codable, Sendable {
    public let title: String?
    public let link: String?
    public let mediaType: String?
    public let enclosureURL: String?
    public let enclosureType: String?
    public let durationSeconds: Int?
    public let thumbnailURL: String?
    public let videoID: String?
    public let videoPlatform: String?
    public let contentSnippet: String?
    public let guid: String?

    public init(
        title: String?,
        link: String?,
        mediaType: String?,
        enclosureURL: String?,
        enclosureType: String?,
        durationSeconds: Int?,
        thumbnailURL: String?,
        videoID: String?,
        videoPlatform: String?
    ) {
        self.title = title
        self.link = link
        self.mediaType = mediaType
        self.enclosureURL = enclosureURL
        self.enclosureType = enclosureType
        self.durationSeconds = durationSeconds
        self.thumbnailURL = thumbnailURL
        self.videoID = videoID
        self.videoPlatform = videoPlatform
        contentSnippet = nil
        guid = nil
    }

    private enum CodingKeys: String, CodingKey {
        case title
        case link
        case mediaType
        case enclosureURL = "enclosureUrl"
        case enclosureType
        case durationSeconds
        case thumbnailURL = "thumbnailUrl"
        case videoID = "videoId"
        case videoPlatform
        case contentSnippet
        case guid
    }

    public func encode(to encoder: any Encoder) throws {
        var object: [String: DecodedJSONValue] = [:]
        object["title"] = title.map(DecodedJSONValue.string)
        object["link"] = link.map(DecodedJSONValue.string)
        object["media_type"] = mediaType.map(DecodedJSONValue.string)
        object["enclosure_url"] = enclosureURL.map(DecodedJSONValue.string)
        object["enclosure_type"] = enclosureType.map(DecodedJSONValue.string)
        object["duration_seconds"] = durationSeconds.map { .number(Double($0)) }
        object["thumbnail_url"] = thumbnailURL.map(DecodedJSONValue.string)
        object["videoId"] = videoID.map(DecodedJSONValue.string)
        object["video_platform"] = videoPlatform.map(DecodedJSONValue.string)
        object["contentSnippet"] = contentSnippet.map(DecodedJSONValue.string)
        object["guid"] = guid.map(DecodedJSONValue.string)
        var container = encoder.singleValueContainer()
        try container.encode(object)
    }
}

public struct RssFeedItemURLModel: Codable, Sendable {
    public let entityType: String?
    public let id: String?
    public let url: String
    @RequiredNullable
    public var canonicalUrlId: String?
    public let hostname: Hostname?
    public let pathname: String?
    public let searchParams: [String: String]?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, url, canonicalUrlId, hostname, pathname, searchParams
    }

    public init(url: String) {
        entityType = nil
        id = nil
        self.url = url
        canonicalUrlId = nil
        hostname = nil
        pathname = nil
        searchParams = nil
    }
}
