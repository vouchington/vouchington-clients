import Foundation

public struct FeatureFlagsResponse: Codable, Sendable {
    public let flags: [String: Bool]
    public let overrides: [String: Bool]?
}

public struct CaptchaConfigResponse: Codable, Sendable {
    public let alwaysApprove: Bool
}

public struct CommunityMembership: Decodable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let userId: String
    public let role: String
    public let approvedById: String?
    public let createdAt: Date
    public let updatedAt: Date
    public let removedAt: Date?
    public let removedById: String?
}

public struct ReferralClickLogResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct ReferralClickLogEntry: Codable, Identifiable, Sendable {
    public let id: String
    public let landingUrl: String
    public let signedUpAt: Date?
    public let userId: String?
    public let createdAt: Date
}

public struct ReferralClickLogUser: Codable, Identifiable, Sendable {
    public let id: String
    public let username: String?
    public let roles: [String]?
    public let profileImageId: String?
    public let markdown: String?
    @RequiredNullable public var accountType: AccountType?
}

public struct ReferralClickLogResponse: Codable, Sendable {
    public let results: [ReferralClickLogResult]
    public let clicks: [String: ReferralClickLogEntry]
    public let users: [String: ReferralClickLogUser]
    public let pageInfo: Page<ReferralClickLogResult>.PageInfo
}

public struct ReferralLinkFeedResponse: Codable, Sendable {
    public let results: [NativeReferralLink]
    public let pageInfo: Page<NativeReferralLink>.PageInfo?
    public let users: [String: ReferralLinkUser]?
}

public struct NativeReferralLink: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String?
    public let referralProgramId: String?
    public let urlId: String?
    public let label: String?
    public let url: String?
    public let activatedAt: Date?
    public let deactivatedAt: Date?
    public let createdAt: Date?
    public let updatedAt: Date?
    public let referralProgramName: String?
    public let referralProgramSlug: String?
    public let consecutiveCrawlFailures: Int?
    public let deletedAt: Date?
    public let lastCrawlFailureAt: Date?
    public let lastCrawlId: String?
    public let lastCrawlSuccessAt: Date?
    public let parentLinkId: String?
    public let unfurlRequestedAt: Date?
    public let unfurlCompletedAt: Date?
    public let unfurlFailedAt: Date?
    public let unfurlLastError: String?
}

public struct PrioritizedReferralLink: Codable, Identifiable, Sendable {
    public let id: String
    public let userId: String?
    public let isOfficial: Bool
    public let referralProgramId: String
    public let url: String
    public let label: String?
    public let priorityGroup: Int
    public let contributionRank: Int
    public let tierRank: Int
    public let bestScore: Double
    public let reviewPostId: String?
    public let reviewPostSlug: String?
    public let reviewAvgRating: Double?
}

public struct ReferralLinkUser: Decodable, Encodable, Identifiable, Sendable {
    public let id: String
    public let username: String
    public let displayName: String?
    public let profileImageId: String?
    @RequiredNullable public var accountType: AccountType?

    private enum CodingKeys: String, CodingKey {
        case id
        case username
        case displayName
        case profileImageId
        case accountType
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(username, forKey: .username)
        try container.encodeIfPresent(displayName, forKey: .displayName)
        try container.encode(profileImageId, forKey: .profileImageId)
        try container.encode(accountType, forKey: .accountType)
    }
}

public struct PrioritizedReferralLinksResponse: Codable, Sendable {
    public let links: [PrioritizedReferralLink]
    public let users: [String: ReferralLinkUser]
}

public struct ReferralProgramValidationInfoResponse: Codable, Sendable {
    public let validationInfo: ReferralProgramValidationInfo
}

public struct ReferralProgramValidationInfo: Codable, Sendable {
    public let userHelpText: String?
    public let exampleUrls: [String]
}

public struct TopicRecommendationsResponse: Codable, Sendable {
    public let results: [NativeTopicRecommendation]
    public let pageInfo: Page<NativeTopicRecommendation>.PageInfo?
}

public struct NativeTopicRecommendation: Codable, Identifiable, Sendable {
    public let id: String
    public let title: String?
    public let markdown: String?
}

public struct EntityBookmarksResponse: Codable, Sendable {
    public let bookmarks: [String: Bool]
}

public struct DirectConversation: Codable, Identifiable, Sendable {
    public let id: String
    public let channelType: String?
    public let title: String
    public let createdAt: Date
    public let createdById: String?
    public let updatedAt: Date
    public var participantUsernames: [String]?
    public let participantAddPolicy: ConversationParticipantAddPolicy?
}

public struct DirectMessage: Codable, Identifiable, Sendable {
    public let id: String
    public let conversationId: String
    public let bodyText: String
    public let createdById: String?
    public let senderUsername: String?
    public let createdAt: Date
    public let updatedAt: Date
    public let deletedAt: Date?
}

public struct ConversationParticipant: Codable, Identifiable, Sendable {
    public let id: String
    public let conversationId: String
    public let userId: String?
    public let role: String
    public let createdAt: Date
    public let removedAt: Date?
    public let username: String?
    public let profileImageId: String?
}
