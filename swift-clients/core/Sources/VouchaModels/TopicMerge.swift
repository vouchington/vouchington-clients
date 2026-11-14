import Foundation

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
