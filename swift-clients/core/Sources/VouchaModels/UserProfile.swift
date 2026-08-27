import Foundation

public struct UserProfileResponse: Codable, Sendable {
    public let user: PublicUser
    public let userBioHtml: String?
    public let userMetrics: UserProfileMetrics?
    public let profileLinks: [ProfileLink]
    public let userVouchElection: UserTrustElection?
}

public struct UserTrustElection: Codable, Sendable {
    public let votesCountUp: Int
    public let votesCountDown: Int
    public let votesScoreNet: Double
}

public struct UserTrustContext: Codable, Sendable {
    public let positiveByFollowing: UserTrustContextGroup
    public let negativeByFollowing: UserTrustContextGroup
    public let electionVote: ElectionVote?
}

public struct UserTrustContextGroup: Codable, Sendable {
    public let total: Int
    public let users: [PublicUser]
}

public struct UserProfileMetrics: Codable, Sendable {
    public let id: String
    public let count: [String: Int]
    public let viewerCount: [String: Int]?
    public let bookmarkers: [String: Int]?

    public func visibleCount(_ key: String) -> Int {
        viewerCount?[key] ?? count[key] ?? 0
    }
}

public struct UserProfilePostFeedResponse: Codable, Sendable {
    public let results: [UserProfilePostResult]
    public let pageInfo: Page<UserProfilePostResult>.PageInfo
    public let posts: [String: Post]
    public let postsMetrics: [String: PostMetrics]
    public let postElections: [String: PostElection]
    public let electionVotes: [String: PostVote]?
    public let markdownToHtml: [String: String]?
}

public struct UserProfilePostResult: Codable, Identifiable, Sendable {
    public let id: String
    public let postType: PostType
}
