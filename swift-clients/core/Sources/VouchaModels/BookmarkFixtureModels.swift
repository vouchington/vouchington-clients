public struct BookmarkRssFeedItemsResponse: Codable, Sendable {
    public let results: [RssFeedItem]
    public let pageInfo: Page<RssFeedItem>.PageInfo
    public let rssFeedItemThumbnailUrl: DecodedJSONValue
    public let rssFeedItemEmbeds: [String: UrlEmbed]?
}

public struct BookmarkRssFeedsResponse: Codable, Sendable {
    public let results: [RssFeedSource]
    public let pageInfo: Page<RssFeedSource>.PageInfo
    public let topicElections: [String: RssFeedElectionSummary]
    public let hostnameElections: [String: RssFeedElectionSummary]
}
