import Foundation

/// `POST /api/v1/posts` response for a `topic_recommendation` post.
public struct TopicRecommendationPostEnvelope: Codable, Sendable {
    public let post: TopicRecommendationPost
    public let postElection: PostElection?
    public let electionVote: PostVote?
}

public struct TopicRecommendationPost: Codable, Identifiable, Sendable {
    public let id: String
    public let postType: PostType
    public let slug: String?
    public let title: String?
    public let markdown: String?
    public let isAnonymous: Bool?
    public let createdById: String?
    @TolerantNullable
    public var parentPostId: String?
    @TolerantNullable
    public var rootPostId: String?
    public let createdAt: Date?
    public let updatedAt: Date?
    public let topicRecommendation: TopicRecommendationDetails?
}

public struct TopicRecommendationDetails: Codable, Sendable {
    public let postId: String?
    public let status: String?
    public let topicTitle: String?
    public let topicSlug: String?
    public let topicMarkdown: String?
    public let topicType: String?
    public let aliases: [String]?
    @TolerantNullable
    public var hostname: TopicRecommendationHostname?
    @TolerantNullable
    public var hostnameId: String?
    public let hostnames: [TopicRecommendationHostname]?
    public let landingPageUrls: [String]?
    @TolerantNullable
    public var exampleReferralLink: String?
    @TolerantNullable
    public var createdTopicId: String?
    @TolerantNullable
    public var createdTopicSlug: String?
    @TolerantNullable
    public var approvalErrorMessage: String?
    @TolerantNullable
    public var rejectionReason: String?
    @TolerantNullable
    public var reviewedAt: Date?
    @TolerantNullable
    public var reviewedById: String?
}

public struct TopicRecommendationHostname: Codable, Sendable {
    public let id: String?
    public let hostname: String
}
