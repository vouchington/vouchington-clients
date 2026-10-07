import VouchaModels

struct RssFeedPage: Decodable {
    struct ResultItem: Decodable {
        let id: String
        let entityId: String?
        let storyId: String?
        let deliveryType: String?
    }

    struct PageInfo: Decodable {
        let hasNextPage: Bool
        let endCursor: String?
    }

    let results: [ResultItem]
    let pageInfo: PageInfo
    let rssFeedItems: [String: RssFeedItem]
    let rssFeedItemThumbnailUrl: [String: String]?
    let rssFeedItemEmbeds: [String: UrlEmbed]?
    let rssFeedItemElections: [String: RssFeedItemElection]?
    let electionVotes: [String: ElectionVote]?
    let bookmarks: [String: [String: Bool]]?
    let storyMemberPages: [String: StoryMemberPage]?
    let storyPostIds: [String: String]?
}
