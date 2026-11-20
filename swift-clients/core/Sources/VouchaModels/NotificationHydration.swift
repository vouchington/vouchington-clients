import Foundation

public struct NotificationActor: Codable, Sendable, Equatable {
    public let id: String
    public let username: String
}

/// Live post facts; an anonymous post has no actor and a removed post is absent.
public struct NotificationPost: Codable, Sendable {
    public let id: String
    public let postType: String
    public let snippet: String
    @RequiredNullable
    public var actorUserId: String?
    @RequiredNullable
    public var communityId: String?

    private enum CodingKeys: String, CodingKey {
        case id, postType, snippet, actorUserId, communityId
    }
}

public struct NotificationRssFeedItem: Codable, Sendable, Equatable {
    public let id: String
    public let feedName: String
    public let snippet: String
}

public struct NotificationCopyrightNotice: Codable, Sendable, Equatable {
    public let id: String
    public let jurisdiction: String
}

/// Facts read live by id for one page of notifications; a missing key means the source is gone.
public struct NotificationHydration: Codable, Sendable {
    public var communities: [String: NotificationCommunity]
    public var actors: [String: NotificationActor]
    public var posts: [String: NotificationPost]
    public var rssFeedItems: [String: NotificationRssFeedItem]
    public var copyrightNotices: [String: NotificationCopyrightNotice]
    /// Moderator-authored text keyed by its owning row id.
    public var publicTexts: [String: String]

    public init(
        communities: [String: NotificationCommunity] = [:],
        actors: [String: NotificationActor] = [:],
        posts: [String: NotificationPost] = [:],
        rssFeedItems: [String: NotificationRssFeedItem] = [:],
        copyrightNotices: [String: NotificationCopyrightNotice] = [:],
        publicTexts: [String: String] = [:]
    ) {
        self.communities = communities
        self.actors = actors
        self.posts = posts
        self.rssFeedItems = rssFeedItems
        self.copyrightNotices = copyrightNotices
        self.publicTexts = publicTexts
    }

    private enum CodingKeys: String, CodingKey {
        case communities, actors, posts, rssFeedItems, copyrightNotices, publicTexts
    }

    /// Decodes whichever hydration maps the page carries; absent maps are empty.
    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        communities = try container.decodeIfPresent([String: NotificationCommunity].self, forKey: .communities) ?? [:]
        actors = try container.decodeIfPresent([String: NotificationActor].self, forKey: .actors) ?? [:]
        posts = try container.decodeIfPresent([String: NotificationPost].self, forKey: .posts) ?? [:]
        rssFeedItems = try container.decodeIfPresent(
            [String: NotificationRssFeedItem].self, forKey: .rssFeedItems
        ) ?? [:]
        copyrightNotices = try container.decodeIfPresent(
            [String: NotificationCopyrightNotice].self, forKey: .copyrightNotices
        ) ?? [:]
        publicTexts = try container.decodeIfPresent([String: String].self, forKey: .publicTexts) ?? [:]
    }
}
