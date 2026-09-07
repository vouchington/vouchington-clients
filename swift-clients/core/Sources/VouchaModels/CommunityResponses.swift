import Foundation

public struct CommunityResponse: Codable, Sendable {
    public let community: Community
    public let communityMetrics: CommunityMetrics?
    public let membership: CommunityMember?
    public let user: CommunityOwner?
    public let hasPendingApplication: Bool?
}

public struct CommunityOwner: Codable, Identifiable, Sendable {
    public let id: String
    public let username: String?
}

public struct CommunitiesSearchResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunitiesSearchResponse: Codable, Sendable {
    public let results: [CommunitiesSearchResult]
    public let pageInfo: Page<CommunitiesSearchResult>.PageInfo
    public let communities: [String: Community]
    public let communityMetrics: [String: CommunityMetrics]?
    public let users: [String: CommunityOwner]?
    public let communityMemberships: [String: CommunityMember]?
    public let pendingApplicationCommunityIds: [String]?
    public let bookmarks: [String: [String: Bool]]?
}

public struct CommunityMembersResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityMembersResponse: Codable, Sendable {
    public let results: [CommunityMembersResult]
    public let pageInfo: Page<CommunityMembersResult>.PageInfo
    public let communityMembers: [String: CommunityMember]
    public let users: [String: PublicUser]
}

public struct CommunityApplicationsResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityApplicationsResponse: Codable, Sendable {
    public let results: [CommunityApplicationsResult]
    public let pageInfo: Page<CommunityApplicationsResult>.PageInfo
    public let communityApplications: [String: CommunityApplication]
}

public struct CommunityApplicationQuestionsResponse: Codable, Sendable {
    public let questions: [CommunityApplicationQuestion]
}

public struct CommunityPinnedPostsResponse: Codable, Sendable {
    public let pinnedPosts: [CommunityPinnedPost]
}

public struct CommunityPendingReportsResponse: Codable, Sendable {
    public let reports: [CommunityPendingReport]
    public let pageInfo: Page<CommunityPendingReport>.PageInfo
}

public struct CommunityPendingReport: Codable, Identifiable, Sendable {
    public let id: String
    public let caseId: String
    public let entityType: String
    public let entityId: String
    public let targetLabel: String?
    public let targetPath: String?
    @RequiredNullable public var targetContent: AuthoredContentText?
    public let reason: String
    public let status: ModerationReportStatus
    public let reportCount: Int
    public let createdAt: Date
    @RequiredNullable
    public var reviewedAt: Date?
    public let targetUserId: String?
    public let reporterUserId: String?
    public let reporterUsername: String?
    public let note: String?
    @RequiredNullable
    public var resolvedById: String?
    public let adminActionPath: String?
    public let targetAvailable: Bool?
    @RequiredNullable
    public var judgement: ModerationReportJudgement?
    @RequiredNullable
    public var communityBanEvasion: CommunityBanEvasionContext?
    public let claim: CommunityModerationQueueClaim?
    public let escalatedAt: Date?
    public let escalatedById: String?
}

public struct CommunityListItemCountsResponse: Codable, Sendable {
    public let topic: Int
    public let rssFeed: Int
    public let post: Int
    public let urlHostname: Int
    public let url: Int

    public var counts: CommunityListItemCounts {
        CommunityListItemCounts(
            topic: topic,
            rssFeed: rssFeed,
            post: post,
            urlHostname: urlHostname,
            url: url
        )
    }
}

public struct CommunityListItemsResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityListItemsResponse: Codable, Sendable {
    public let results: [CommunityListItemsResult]
    public let pageInfo: Page<CommunityListItemsResult>.PageInfo
    public let communityListItems: [String: CommunityListItem]
}

public struct CommunityInvitesResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityInvitesResponse: Codable, Sendable {
    public let results: [CommunityInvitesResult]
    public let pageInfo: Page<CommunityInvitesResult>.PageInfo
    public let communityInvites: [String: CommunityInvite]
}

public struct CommunityBansResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityBansResponse: Codable, Sendable {
    public let results: [CommunityBansResult]
    public let pageInfo: Page<CommunityBansResult>.PageInfo
    public let communityBans: [String: CommunityBan]
    public let users: [String: PublicUser]
}

public struct CommunityRaidModeSuggestion: Codable, Sendable {
    public let velocitySpike: Bool
    public let flagCount: Int
    @RequiredNullable
    public var latestFlaggedAt: Date?
}

public struct CommunityRestrictionsResult: Codable, Identifiable, Sendable {
    public let id: String
}

public struct CommunityRestrictionsResponse: Codable, Sendable {
    public let results: [CommunityRestrictionsResult]
    public let pageInfo: Page<CommunityRestrictionsResult>.PageInfo
    public let communityRestrictions: [String: CommunityRestriction]
    public let raidModeSuggestion: CommunityRaidModeSuggestion?
}

public struct ActivateCommunityRestrictionsResponse: Codable, Sendable {
    public let communityRestrictions: [String: CommunityRestriction]
}
