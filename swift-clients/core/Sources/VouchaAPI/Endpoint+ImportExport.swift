import Foundation
import VouchaModels

public extension Endpoint {
    static func importTopics(_ names: [String]) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/import/topics", body: TopicImportBody(names: names))
    }

    static var exportTopics: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/export/topics")
    }

    static var exportTopicsDownload: Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/my/export/topics",
            queryItems: [.init(name: "download", value: "1")]
        )
    }

    static func importRssFeeds(_ input: SourceImportInput) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/import/rss-feeds", body: input)
    }

    static func rssFeedImportStatus(importId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/my/import/rss-feeds/\(pathSegment(importId))")
    }

    static func exportRssFeeds(feedType: SourceFeedType?, format: SourceExportFormat) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let feedType {
            queryItems.append(.init(name: "feed_type", value: feedType.rawValue))
        }
        if format == .csv {
            queryItems.append(.init(name: "format", value: format.rawValue))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/my/export/rss-feeds",
            queryItems: queryItems,
            headers: ["Accept": format == .csv ? "text/csv" : "text/xml"]
        )
    }
}
