import Foundation

public struct Story: Codable, Identifiable, Sendable {
    public let id: String
    public let title: String?
    public let clusterReason: String?
    public let publishedAt: Date?
    public let officialRssFeedItemId: String?
    public let officialLockedAt: Date?
    public let createdAt: Date
    public let updatedAt: Date
    public let deletedAt: Date?

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(title, forKey: .title)
        try container.encode(clusterReason, forKey: .clusterReason)
        try container.encode(publishedAt, forKey: .publishedAt)
        try container.encode(officialRssFeedItemId, forKey: .officialRssFeedItemId)
        try container.encode(officialLockedAt, forKey: .officialLockedAt)
        try container.encode(createdAt, forKey: .createdAt)
        try container.encode(updatedAt, forKey: .updatedAt)
        try container.encode(deletedAt, forKey: .deletedAt)
    }

    private enum CodingKeys: String, CodingKey {
        case id, title, clusterReason, publishedAt, officialRssFeedItemId, officialLockedAt
        case createdAt, updatedAt, deletedAt
    }
}

public struct PostStory: Codable, Sendable {
    public let postId: String
    public let storyId: String
    public let initiatedById: String
    public let createdAt: Date
}

public struct StoryPostResult: Codable, Sendable {
    private enum CodingKeys: String, CodingKey {
        case post
        case story
        case postStory
    }

    public let post: Post
    public let story: Story
    public let postStory: PostStory
}
