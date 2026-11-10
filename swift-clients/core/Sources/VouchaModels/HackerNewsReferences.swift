import Foundation

public struct HackerNewsReferences: Codable, Sendable, Equatable {
    public let commentCount: Int
    public let storyCount: Int
    public let comments: [HackerNewsCommentReference]?
    public let stories: [HackerNewsStoryReference]?
}

public struct HackerNewsCommentReference: Codable, Sendable, Equatable {
    public let hackerNewsItemId: Int
    public let parentStoryItemId: Int
    public let postedAt: Date
}

public struct HackerNewsStoryReference: Codable, Sendable, Equatable {
    public let hackerNewsItemId: Int
    public let points: Int
    public let postedAt: Date
    public let replyCount: Int
}

/// Response of `POST /api/v1/topics/:source/merges` and `GET /api/v1/topics/:source/merges/:operation`.
public struct TopicMergeEnvelope: Codable, Sendable, Equatable {
    public let topicMerge: TopicMergeOperation
}

public struct TopicMergeOperation: Codable, Sendable, Equatable {
    public let id: String
    public let sourceTopicId: String
    public let destinationTopicId: String
    public let status: String
    public let stage: String
    public let errorCode: String?
    public let createdAt: Date
    public let completedAt: Date?
    public let statusUrl: String
}
