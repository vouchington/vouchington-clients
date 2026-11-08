import Foundation

public struct CommunityApplicationsResult: Codable, Identifiable, Sendable {
    public let id: String
    public let entityType: String?
    private enum CodingKeys: String, CodingKey { case id
        case entityType = "__entityType"
    }
}

public struct CommunityApplicationSummary: Codable, Identifiable, Sendable {
    public let entityType: String
    public let id: String
    public let userId: String
    public let status: String
    @RequiredNullable
    public var messagePreview: String?
    public let answerCount: Int
    public let createdAt: Date
    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, userId, status, messagePreview, answerCount, createdAt
    }
}

public struct CommunityApplicationsResponse: Codable, Sendable {
    public let results: [CommunityApplicationsResult]
    public let pageInfo: Page<CommunityApplicationsResult>.PageInfo
    public let communityApplicationSummaries: [String: CommunityApplicationSummary]
    public let users: [String: PublicUser]
}

public struct CommunityApplicationResponse: Codable, Sendable {
    public let communityApplication: CommunityApplication
}
