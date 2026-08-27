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
