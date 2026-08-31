import Foundation
import VouchaAPI
import VouchaModels

public struct ImportExportService: Sendable {
    public var importTopics: @Sendable ([String]) async throws -> TopicImportResponse
    public var importSources: @Sendable (SourceImportInput) async throws -> RssFeedImportSubmission
    public var sourceStatus: @Sendable (String) async throws -> RssFeedImportStatus
    public var downloadExport: @Sendable (ImportExportRoute, SourceFeedType?, SourceExportFormat) async throws -> URL

    public static func live(client: APIClient) -> ImportExportService {
        ImportExportService(
            importTopics: { try await client.send(.importTopics($0)) },
            importSources: { try await client.send(.importRssFeeds($0)) },
            sourceStatus: { try await client.send(.rssFeedImportStatus(importId: $0)) },
            downloadExport: { route, feedType, format in
                if route.isTopics {
                    return try await client.download(for: .exportTopicsDownload, filename: "topics.json")
                }
                let filename = format == .csv ? "rss-feeds.csv" : "rss-feeds.opml"
                return try await client.download(
                    for: .exportRssFeeds(feedType: feedType, format: format),
                    filename: filename
                )
            }
        )
    }
}
