public struct PostAuthorAside: Codable, Sendable {
    public let aboutHtml: String
    public let isFollowing: Bool
    public let profileLinks: [ProfileLink]
}

public struct PostEnvelope: Codable, Sendable {
    public let post: Post
    public let authorAside: PostAuthorAside?
    public let html: String?
    public let postMetrics: PostMetrics?
    public let postElection: PostElection?
    public let electionVote: PostVote?
    public let bookmarks: [String: [String: Bool]]?
}

public struct PostThreadEnvelope: Codable, Sendable {
    public let results: [PostThreadResult]
    public let pageInfo: PostThreadPageInfo
    public let posts: [String: Post]
    public let users: [String: PublicUser]?
    public let postsMetrics: [String: PostMetrics]?
    public let postElections: [String: PostElection]?
    public let electionVotes: [String: PostVote]?
    public let markdownToHtml: [String: String]?
    public let bookmarks: [String: [String: Bool]]?
    public let communities: [String: DecodedJSONValue]?
    public let stories: [String: Story]?
    public let rssFeedItems: [String: RssFeedItem]?
    public let rssFeedItemElections: [String: RssFeedItemElection]?
    public let rssFeedItemThumbnailUrl: [String: String]?
    public let relatedPostsByUrlId: [String: [String]]?
    public let storyMemberIds: [String: [String]]?
    public let storyPostIds: [String: [String]]?
    public let pinnedPostIds: [String]?
    public let postLinkEmbeds: [String: DecodedJSONValue]?
}

public struct PostThreadResult: Codable, Sendable {
    public let entityId: String

    enum CodingKeys: String, CodingKey {
        case entityId
        case id
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        if let entityId = try container.decodeIfPresent(String.self, forKey: .entityId) {
            self.entityId = entityId
        } else {
            entityId = try container.decode(String.self, forKey: .id)
        }
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(entityId, forKey: .entityId)
    }
}

public struct PostThreadPageInfo: Codable, Sendable {
    public let hasNextPage: Bool
    public let hasPreviousPage: Bool?
    public let startCursor: String?
    public let endCursor: String?
}
