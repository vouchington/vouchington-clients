import VouchaModels

struct RssFeedPage: Decodable {
    struct ResultItem: Decodable {
        let id: String
        let entityId: String?
        let storyId: String?
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
    let storyMemberIds: [String: [String]]?
    let storyPostIds: [String: String]?
}
