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
