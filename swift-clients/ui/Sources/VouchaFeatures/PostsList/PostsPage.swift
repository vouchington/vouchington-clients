import VouchaModels

struct PostsPage: Decodable {
    struct ResultItem: Decodable {
        let entityId: String
    }

    struct PageInfo: Decodable {
        let hasNextPage: Bool
        let endCursor: String?
    }

    let results: [ResultItem]
    let pageInfo: PageInfo
    let posts: [String: Post]
    let postsMetrics: [String: PostMetrics]
    let postElections: [String: PostElection]
    let markdownToHtml: [String: String]?
    let electionVotes: [String: PostVote]?
    let bookmarks: [String: [String: Bool]]?

    struct PostVote: Decodable {
        let choice: ElectionVoteChoice
    }
}
