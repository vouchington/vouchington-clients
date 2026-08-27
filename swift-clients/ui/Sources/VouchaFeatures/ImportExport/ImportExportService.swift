import Foundation
import VouchaAPI
import VouchaModels

public struct ImportExportService: Sendable {
    public var importTopics: @Sendable ([String]) async throws -> TopicImportResponse
    public var exportTopics: @Sendable () async throws -> TopicExportResponse
    public var importSources: @Sendable (SourceImportInput) async throws -> RssFeedImportSubmission
    public var sourceStatus: @Sendable (String) async throws -> RssFeedImportStatus
    public var exportSources: @Sendable (SourceFeedType?, SourceExportFormat) async throws -> Data

    public static func live(client: APIClient) -> ImportExportService {
        ImportExportService(
            importTopics: { try await client.send(.importTopics($0)) },
            exportTopics: { try await client.send(.exportTopics) },
            importSources: { try await client.send(.importRssFeeds($0)) },
            sourceStatus: { try await client.send(.rssFeedImportStatus(importId: $0)) },
            exportSources: { try await client.data(for: .exportRssFeeds(feedType: $0, format: $1)) }
        )
    }
}
