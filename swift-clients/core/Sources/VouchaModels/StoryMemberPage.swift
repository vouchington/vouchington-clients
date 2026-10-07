public struct StoryMemberPage: Codable, Sendable {
    public let itemIds: [String]
    public let pageInfo: Page<RssFeedItem>.PageInfo

    public init(itemIds: [String], pageInfo: Page<RssFeedItem>.PageInfo) {
        self.itemIds = itemIds
        self.pageInfo = pageInfo
    }
}

public struct StoryPageResponse: Codable, Sendable {
    public let story: Story
    public let itemIds: [String]
    public let pageInfo: Page<RssFeedItem>.PageInfo
    public let rssFeedItems: [String: RssFeedItem]
    public let rssFeedItemElections: [String: RssFeedItemElection]?
    public let rssFeedItemEmbeds: [String: UrlEmbed]?
    public let rssFeedItemThumbnailUrl: [String: String]?
    public let rssFeedItemContentHtml: [String: String]?
    public let relatedPostsByUrlId: [String: [String]]?
    public let posts: [String: Post]?
    public let postsMetrics: [String: PostMetrics]?
    public let storyPostIds: [String: String]?
    public let bookmarks: [String: [String: Bool]]?
    public let electionVotes: [String: ElectionVote]?
    public let rssFeedBookmarks: [String: [String: Bool]]?
    public let users: [String: PublicUser]?
}
