import Foundation

public enum ListVisibility: String, Codable, Sendable {
    case `private`
    case unlisted
    case `public`
}

public enum ListItemType: String, Codable, Sendable {
    case rssFeedItem = "rss_feed_item"
    case post
}

public struct UserList: Codable, Identifiable, Sendable {
    public let id: String
    public let ownerUserId: String
    public let name: String
    public let description: String?
    public let visibility: ListVisibility
    public let createdAt: Date
    public let updatedAt: Date
    public let removedAt: Date?
    public let contentProvenance: PublicContentProvenance?
}

public struct ListItem: Codable, Identifiable, Sendable {
    public let id: String
    public let listId: String
    public let itemType: ListItemType
    public let entityId: String
    public let orderIndex: Int?
    public let createdAt: Date
    public let mediaType: String?
}

public struct ListReference: Codable, Identifiable, Sendable {
    public let id: String
}

public struct ListItemReference: Codable, Identifiable, Sendable {
    public let id: String
}

public struct ListsSearchResponse: Codable, Sendable {
    public let results: [ListReference]
    public let pageInfo: Page<ListReference>.PageInfo
    public let lists: [String: UserList]
}

public struct ListItemsResponse: Codable, Sendable {
    public let results: [ListItemReference]
    public let pageInfo: Page<ListItemReference>.PageInfo
    public let listItems: [String: ListItem]
}

public struct ListResponse: Codable, Sendable {
    public let list: UserList
}

public struct ListItemResponse: Codable, Sendable {
    public let listItem: ListItem
}

public struct ListsContainingResponse: Codable, Sendable {
    public let listIds: [String]
}

public struct ImportCommunityListResponse: Codable, Sendable {
    public let posts: Int
    public let items: Int
}
