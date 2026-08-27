import Foundation
import VouchaLocalization
import VouchaModels

struct NativeRssFeedItemsResponse: Decodable {
    let results: [NativeRssFeedItemResult]
    let pageInfo: Page<FixtureReference>.PageInfo?
    let rssFeedItems: [String: NativeRssFeedItemSummary]
}

struct NativeRssFeedsTrendingResponse: Decodable {
    let results: [NativeIdentifiedResult]
    let rssFeeds: [String: NativeRssFeedSummary]
}

struct NativeRssFeedsPageResponse: Decodable {
    let results: [NativeRssFeedSummary]
    let pageInfo: Page<FixtureReference>.PageInfo?
}

struct NativeRssFeedCrawlsResponse: Decodable {
    let results: [NativeRssFeedCrawl]
    let pageInfo: Page<NativeRssFeedCrawl>.PageInfo
}

struct NativeRssFeedCrawlResponse: Decodable {
    let crawl: NativeRssFeedCrawl
}

struct NativeRssFeedCrawl: Decodable, Identifiable {
    let id: String
    let responseCode: Int?
    let createdAt: String?

    var statusText: UiVerbatimText {
        if let responseCode {
            return .message(.nativeSwiftRouteSurfaceHttpStatus, numberParameters: ["code": Double(responseCode)])
        }
        return .message(.nativeSwiftRouteSurfaceStatusUnavailable)
    }
}

struct NativeRssFeedItemResult: Decodable {
    let id: String
    let entityId: String?
}

struct NativeRssFeedItemSummary: Decodable, Identifiable {
    let id: String
    let title: String?
    let link: String?
    let mediaType: String?
    let publishedAt: Date?
    let rssFeed: NativeRssFeedSummary?
    let data: NativeRssFeedItemData?

    private enum CodingKeys: String, CodingKey {
        case id
        case title
        case link
        case mediaType
        case publishedAt
        case rssFeed
        case mediaContent
        case data
    }

    private enum MediaContentCodingKeys: String, CodingKey {
        case medium
        case type
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        title = try container.decodeIfPresent(String.self, forKey: .title)
        link = try container.decodeIfPresent(String.self, forKey: .link)
        publishedAt = try container.decodeIfPresent(Date.self, forKey: .publishedAt)
        rssFeed = try container.decodeIfPresent(NativeRssFeedSummary.self, forKey: .rssFeed)
        data = try container.decodeIfPresent(NativeRssFeedItemData.self, forKey: .data)
        if let directMediaType = try container.decodeIfPresent(String.self, forKey: .mediaType) {
            mediaType = directMediaType
        } else if let media = try? container.nestedContainer(
            keyedBy: MediaContentCodingKeys.self,
            forKey: .mediaContent
        ) {
            mediaType = try media.decodeIfPresent(String.self, forKey: .medium)
                ?? media.decodeIfPresent(String.self, forKey: .type)
        } else {
            mediaType = nil
        }
    }
}

struct NativeRssFeedItemData: Decodable {
    let title: String?
    let link: String?
}

struct NativeRssFeedSummary: Decodable, Identifiable {
    let id: String
    let title: String?
    let feedType: String
    let rssFeedUrl: NativeRssFeedUrl
    let hostname: NativeHostnameName?
    let topic: NativeRssFeedTopicSummary?

    private enum CodingKeys: String, CodingKey {
        case id
        case title
        case feedType
        case rssFeedUrl
        case hostname
        case topic
    }
}

struct NativeRssFeedTopicSummary: Decodable {
    let id: String?
    let slug: String?
}

struct NativeHostnameName: Decodable {
    let hostname: String
}

struct NativeRssFeedUrl: Decodable {
    let url: String
}
