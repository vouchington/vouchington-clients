import Foundation

public enum CommunityVisibility: String, Codable, Sendable {
    case `public`
    case `private`
}

public enum CommunityMemberRosterVisibility: String, Codable, Sendable {
    case `public`
    case users
    case members
    case moderators
}

public enum CommunityMemberRole: String, Codable, Sendable {
    case owner
    case moderator
    case member
}

public enum CommunityListType: String, Codable, Sendable {
    case follow
    case mute
}

public enum CommunityListItemType: String, Codable, Sendable {
    case topic
    case rssFeed = "rss_feed"
    case post
    case urlHostname = "url_hostname"
    case url

    public var routeSegment: String {
        switch self {
        case .topic:
            "topics"
        case .rssFeed:
            "rss-feeds"
        case .post:
            "posts"
        case .urlHostname:
            "domains"
        case .url:
            "urls"
        }
    }
}

public enum CommunityRestrictionType: String, Codable, Sendable {
    case requirePostApproval = "require_post_approval"
    case noNewMemberPosts = "no_new_member_posts"
    case noLinks = "no_links"
    case approvedMembersOnly = "approved_members_only"
}

public enum CommunityApplicationQuestionFieldType: String, Codable, Sendable {
    case shortText = "short_text"
    case longText = "long_text"
    case singleSelect = "single_select"
    case multiSelect = "multi_select"
    case checkbox
}

public struct CommunityMetrics: Codable, Identifiable, Sendable {
    public let id: String
    public let memberCount: Int
    public let postCount: Int
    public let listItemCount: Int
    public let proxyFollowCount: Int
    public let proxyMuteCount: Int
    public let virtualSubscriptionCount: Int
}

public struct CommunityMember: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let userId: String
    public let role: CommunityMemberRole
    public let approvedById: String?
    public let createdAt: Date
    public let updatedAt: Date
    public let removedAt: Date?
    public let removedById: String?
}

public struct CommunityListItem: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let itemType: CommunityListItemType
    public let entityId: String
    public let orderIndex: Int
    public let addedById: String?
    public let createdAt: Date
}

public struct CommunityApplicationQuestion: Codable, Identifiable, Sendable {
    public let entityType: String?
    public let id: String
    public let communityId: String
    public let question: String
    public let fieldType: CommunityApplicationQuestionFieldType
    public let options: [String]?
    public let orderIndex: Int
    public let required: Bool
    public let createdAt: Date
    public let updatedAt: Date?
    @RequiredNullable
    public var deletedAt: Date?

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case id, communityId, question, fieldType, options, orderIndex, required, createdAt, updatedAt, deletedAt
    }
}

public struct CommunityApplication: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let userId: String
    public let answers: [String: DecodedJSONValue]
    public let message: String?
    public let reviewedAt: Date?
    public let reviewedById: String?
    public let approvedAt: Date?
    public let rejectedAt: Date?
    public let rejectionReason: String?
    public let createdAt: Date
}

public struct CommunityInvite: Codable, Identifiable, Sendable {
    public let id: String
    public let communityId: String
    public let code: String
    public let invitedUserId: String?
    public let invitedEmail: String?
    public let invitedById: String
    public let acceptedAt: Date?
    public let acceptedByUserId: String?
    public let declinedAt: Date?
    public let revokedAt: Date?
    public let createdAt: Date
}

public struct CommunityPostReview: Codable, Sendable {
    public let communityId: String
    public let postId: String
    public let submittedById: String?
    public let createdAt: Date
    public let reviewedAt: Date?
    public let reviewedById: String?
    public let approvedAt: Date?
    public let rejectedAt: Date?
    public let rejectionReason: String?
    public let unpublishedAt: Date?
    public let unpublishedById: String?
}

public struct CommunityPinnedPost: Codable, Sendable {
    public let entityType: String?
    public let communityId: String
    public let postId: String
    public let orderIndex: Int
    public let pinnedById: String
    public let createdAt: Date

    private enum CodingKeys: String, CodingKey {
        case entityType = "__entityType"
        case communityId, postId, orderIndex, pinnedById, createdAt
    }
}

public struct CommunityListItemCounts: Codable, Sendable {
    public let topic: Int
    public let rssFeed: Int
    public let post: Int
    public let urlHostname: Int
    public let url: Int

    public init(topic: Int, rssFeed: Int, post: Int, urlHostname: Int, url: Int) {
        self.topic = topic
        self.rssFeed = rssFeed
        self.post = post
        self.urlHostname = urlHostname
        self.url = url
    }
}
