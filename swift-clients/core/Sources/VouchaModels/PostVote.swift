import Foundation

public struct PostVote: Codable, Sendable {
    public let entityType: String
    public let entityId: String?
    public let userId: String?
    public let choice: ElectionVoteChoice
    public let createdAt: Date?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case entityId, userId, choice, createdAt
    }
}
