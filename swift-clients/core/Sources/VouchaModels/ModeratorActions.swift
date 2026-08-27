import Foundation

public struct ModeratorAction: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String?
    public let actorId: String?
    public let actionType: String
    public let postId: String?
    public let targetUserId: String?
    public let reportedUserId: String?
    public let reportId: String?
    public let reviewDisputeId: String?
    public let communityApplicationId: String?
    public let reason: String?
    public let metadata: [String: DecodedJSONValue]
    public let createdAt: Date
}

public struct AdminModlogResponse: Codable, Sendable {
    public let results: [IdentifiedResult]
    public let pageInfo: Page<IdentifiedResult>.PageInfo
    public let moderatorActions: [String: ModeratorAction]
    public let users: [String: PublicUser]

    public struct IdentifiedResult: Codable, Identifiable, Sendable {
        public let id: String
    }
}
