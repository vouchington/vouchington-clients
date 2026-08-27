import VouchaAPI
import VouchaModels

extension EndpointManifestCoverage {
    static let importExportEndpoints: [ManifestRegisteredEndpoint] = [
        .init(id: "native.import-export.rss-feeds.submit.default") {
            .importRssFeeds(.urls([
                "https://example.test/feed.xml",
                "https://invalid.example.test/feed.xml"
            ]))
        },
        .init(id: "native.import-export.rss-feeds.status.retrying") {
            .rssFeedImportStatus(importId: "70000000-0000-7000-8000-000000000001")
        },
        .init(id: "native.import-export.rss-feeds.status.partial") {
            .rssFeedImportStatus(importId: "70000000-0000-7000-8000-000000000001")
        },
        .init(id: "native.import-export.topics.import.outcomes") {
            .importTopics(["Travel", "Local News", "Travel", "!!!"])
        },
        .init(id: "native.import-export.topics.export.default") { .exportTopics }
    ]
}
