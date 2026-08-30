import VouchaModels

let nativeImportExportCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.import-export.rss-feeds.submit.default") {
        try assertFixtureCoversDTO($0, as: RssFeedImportSubmission.self, ignoring: ["import.completed_at"])
    },
    RegisteredFixture(id: "native.import-export.rss-feeds.status.retrying") {
        try assertFixtureCoversDTO($0, as: RssFeedImportStatus.self, ignoring: ["import.completed_at"])
    },
    RegisteredFixture(id: "native.import-export.rss-feeds.status.partial") {
        try assertFixtureCoversDTO($0, as: RssFeedImportStatus.self)
    },
    RegisteredFixture(id: "native.import-export.topics.import.outcomes") {
        try assertFixtureCoversDTO($0, as: TopicImportResponse.self)
    },
    RegisteredFixture(id: "native.import-export.topics.export.default") {
        try assertFixtureCoversDTO($0, as: TopicExportResponse.self)
    },
    RegisteredFixture(id: "native.import-export.topics.export.download") {
        try assertFixtureCoversDTO($0, as: [ExportTopic].self)
    }
]
