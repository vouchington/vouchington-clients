import Foundation

public enum ElectionVoteChoice: String, Codable, CaseIterable, Sendable {
    case vouch, like, neutral, dislike, disavow
    case support, oppose
    case confirm, dispute
    case accurate, inaccurate

}

public enum ElectionVotePolicy: Sendable {
    case sentiment, recommendation, relation, moderation

    public var choices: [ElectionVoteChoice] {
        switch self {
        case .sentiment: [.vouch, .like, .neutral, .dislike, .disavow]
        case .recommendation: [.support, .oppose]
        case .relation: [.confirm, .dispute]
        case .moderation: [.accurate, .inaccurate]
        }
    }
}

public struct ElectionVote: Codable, Sendable {
    public let entityType: String
    public let userId: String
    public let entityId: String
    public let choice: ElectionVoteChoice
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case userId, entityId, choice, createdAt
    }
}

public struct TopicElection: Codable, Sendable {
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int
    public let myVote: ElectionVoteChoice?

    public init(votesScoreNet: Double, votesCountUp: Int, votesCountDown: Int, myVote: ElectionVoteChoice? = nil) {
        self.votesScoreNet = votesScoreNet
        self.votesCountUp = votesCountUp
        self.votesCountDown = votesCountDown
        self.myVote = myVote
    }
}

public struct RssFeedItemElection: Codable, Sendable {
    public let entityType: String?
    public let id: String?
    public let votesScoreNet: Double
    public let votesCountUp: Int
    public let votesCountDown: Int
    public let myVote: ElectionVoteChoice?

    public init(votesScoreNet: Double, votesCountUp: Int, votesCountDown: Int, myVote: ElectionVoteChoice? = nil) {
        entityType = nil
        id = nil
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
