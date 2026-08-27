import Foundation

public struct PostMetrics: Codable, Sendable {
    public let entityType: String?
    public let id: String?
    public let updatedAt: Date?
    public let bookmarks: [String: Int]?
    public let count: PostMetricCounts

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, updatedAt, bookmarks, count
    }
}

public struct PostMetricCounts: Codable, Sendable {
    public let descendants: Int
    public let children: Int
    public let ancestors: Int
}

public struct PostElection: Codable, Sendable {
    public let entityType: String?
    public let id: String?
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int
    public let myVote: ElectionVoteChoice?

    public init(
        votesScoreNet: Double,
        votesCountUp: Int,
        votesCountDown: Int,
        myVote: ElectionVoteChoice? = nil,
        entityType: String? = nil,
        id: String? = nil
    ) {
        self.entityType = entityType
        self.id = id
        self.votesScoreNet = votesScoreNet
        self.votesCountUp = votesCountUp
        self.votesCountDown = votesCountDown
        self.myVote = myVote
    }

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, votesScoreNet, votesCountUp, votesCountDown, myVote
    }
}
