import Foundation

public struct RssFeedItemDetailResponse: Codable, Sendable {
    public let rssFeedItem: RssFeedItem
    public let rssFeedItemElection: RssFeedItemElection?
    public let electionVote: ElectionVote?
    public let bookmarks: [String: [String: Bool]]?
    public let contentHtml: String?
    public let rssFeedItemThumbnailUrl: [String: String]?
    public let rssFeedItemEmbeds: [String: UrlEmbed]?
}
