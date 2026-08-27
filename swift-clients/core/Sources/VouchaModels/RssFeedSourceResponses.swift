import Foundation

public struct RssFeedElectionSummary: Codable, Sendable {
    public let id: String
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int
    public let myVote: ElectionVoteChoice?
}

public struct RssFeedSourceListResponse: Codable, Sendable {
    public let results: [RssFeedSource]
    public let pageInfo: Page<RssFeedSource>.PageInfo
    public let topicElections: [String: RssFeedElectionSummary]
    public let hostnameElections: [String: RssFeedElectionSummary]
    public let electionVotes: [String: ElectionVote]?
    public let bookmarks: [String: [String: Bool]]?

    private enum CodingKeys: String, CodingKey {
        case results
        case pageInfo
        case topicElections
        case hostnameElections
        case electionVotes
        case bookmarks
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([RssFeedSource].self, forKey: .results)
        pageInfo = try container.decode(Page<RssFeedSource>.PageInfo.self, forKey: .pageInfo)
        topicElections = try container.decodeIfPresent(
            [String: RssFeedElectionSummary].self,
            forKey: .topicElections
        ) ?? [:]
        hostnameElections = try container.decodeIfPresent(
            [String: RssFeedElectionSummary].self,
            forKey: .hostnameElections
        ) ?? [:]
        electionVotes = try container.decodeIfPresent([String: ElectionVote].self, forKey: .electionVotes)
        bookmarks = try container.decodeIfPresent([String: [String: Bool]].self, forKey: .bookmarks)
    }
}
