import Foundation

public enum SourceFeedType: String, CaseIterable, Codable, Identifiable, Sendable {
    case article, podcast, video, mixed

    public var id: String {
        rawValue
    }
}

public enum SourceExportFormat: String, CaseIterable, Codable, Identifiable, Sendable {
    case csv, opml

    public var id: String {
        rawValue
    }
}

public enum SourceImportInput: Encodable, Sendable, Equatable {
    case urls([String])
    case csv(String)
    case opml(String)

    enum CodingKeys: String, CodingKey { case urls, csv, opml, follow }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case let .urls(urls): try container.encode(urls, forKey: .urls)
        case let .csv(csv): try container.encode(csv, forKey: .csv)
        case let .opml(opml): try container.encode(opml, forKey: .opml)
        }
        try container.encode(true, forKey: .follow)
    }
}

public struct TopicImportBody: Codable, Sendable, Equatable {
    public let names: [String]
    public init(names: [String]) {
        self.names = names
    }
}

public enum ImportResultStatus: String, Codable, Sendable {
    case pending, followed, imported, sourceCreated = "source_created"
    case recommendationCreated = "recommendation_created"
    case alreadyFollowing = "already_following"
    case error
}

public struct ImportResult: Codable, Identifiable, Sendable, Equatable {
    public let id: String?
    public let input: String
    public let status: ImportResultStatus
    public let error: String?
    public let entityId: String?
    public let recommendationPostId: String?

    public init(
        id: String? = nil,
        input: String,
        status: ImportResultStatus,
        error: String? = nil,
        entityId: String? = nil,
        recommendationPostId: String? = nil
    ) {
        self.id = id
        self.input = input
        self.status = status
        self.error = error
        self.entityId = entityId
        self.recommendationPostId = recommendationPostId
    }
}

public struct TopicImportResponse: Codable, Sendable, Equatable {
    public let results: [ImportResult]
    public init(results: [ImportResult]) {
        self.results = results
    }
}

public struct RssFeedImportSummary: Codable, Sendable, Equatable {
    public let id: String
    public let totalRows: Int
    public let completedRows: Int
    public let failedRows: Int
    public let pendingRows: Int
    public let completedAt: Date?
    public let createdAt: Date

    public init(
        id: String,
        totalRows: Int,
        completedRows: Int,
        failedRows: Int,
        pendingRows: Int,
        completedAt: Date?,
        createdAt: Date
    ) {
        self.id = id
        self.totalRows = totalRows
        self.completedRows = completedRows
        self.failedRows = failedRows
        self.pendingRows = pendingRows
        self.completedAt = completedAt
        self.createdAt = createdAt
    }
}

public struct RssFeedImportSubmission: Codable, Sendable, Equatable {
    public let `import`: RssFeedImportSummary
    public let statusUrl: String
    public init(import summary: RssFeedImportSummary, statusUrl: String) {
        self.import = summary
        self.statusUrl = statusUrl
    }
}

public struct RssFeedImportStatus: Codable, Sendable, Equatable {
    public let `import`: RssFeedImportSummary
    public let rows: [ImportResult]
    public init(import summary: RssFeedImportSummary, rows: [ImportResult]) {
        self.import = summary
        self.rows = rows
    }
}

public struct ExportTopic: Codable, Sendable, Equatable {
    public let name: String
    public let slug: String
    public let topicType: String
    public init(name: String, slug: String, topicType: String) {
        self.name = name
        self.slug = slug
        self.topicType = topicType
    }
}

public struct TopicExportResponse: Codable, Sendable, Equatable {
    public let results: [ExportTopic]
    public init(results: [ExportTopic]) {
        self.results = results
    }
}
