public enum EngineeringPsqlJobType: String, Codable, CaseIterable, Sendable {
    case runMigrations
    case runViews
    case runConfigDriven
    case createPartitions
    case cleanupPartitions
}

public struct EngineeringMigrationsResponse: Codable, Sendable {
    public let applied: [String]
    public let pending: [String]
    public let total: Int
}

public struct EngineeringPartitionStatusResponse: Codable, Sendable {
    public let tables: [EngineeringPartitionTable]
}

public struct EngineeringPartitionTable: Codable, Identifiable, Sendable, Hashable {
    public let name: String
    public let partitionCount: Int
    public let totalSizeBytes: Int
    public let partitions: [EngineeringPartitionInfo]

    public var id: String {
        name
    }
}

public struct EngineeringPartitionInfo: Codable, Identifiable, Sendable, Hashable {
    public let name: String
    public let sizeBytes: Int

    public var id: String {
        name
    }
}

public struct ArticleSyncTriggerResponse: Codable, Sendable {
    public let jobId: String
}

public struct ArticleSyncItem: Codable, Identifiable, Sendable, Hashable {
    public let file: String
    public let slug: String
    public let action: String
    public let error: String?

    public var id: String {
        "\(file)|\(slug)"
    }
}

public struct ArticleSyncResult: Codable, Sendable, Hashable {
    public let results: [ArticleSyncItem]
    public let summary: ArticleSyncSummary
}

public struct ArticleSyncSummary: Codable, Sendable, Hashable {
    public let created: Int
    public let updated: Int
    public let skipped: Int
    public let errored: Int
}

public enum ArticleSyncJobStatus: Codable, Sendable, Hashable {
    case active
    case completed(ArticleSyncResult)
    case failed(String)

    private enum CodingKeys: String, CodingKey {
        case status
        case result
        case error
    }

    private enum Status: String, Codable {
        case active
        case completed
        case failed
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        switch try container.decode(Status.self, forKey: .status) {
        case .active:
            self = .active
        case .completed:
            self = try .completed(container.decode(ArticleSyncResult.self, forKey: .result))
        case .failed:
            self = try .failed(container.decode(String.self, forKey: .error))
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .active:
            try container.encode(Status.active, forKey: .status)
        case let .completed(result):
            try container.encode(Status.completed, forKey: .status)
            try container.encode(result, forKey: .result)
        case let .failed(error):
            try container.encode(Status.failed, forKey: .status)
            try container.encode(error, forKey: .error)
        }
    }
}

public struct EngineeringValkeyCacheGroup: Codable, Identifiable, Sendable, Hashable {
    public let name: String
    public let prefixes: [String]

    public var id: String {
        name
    }
}

public struct EngineeringValkeyCacheGroupsResponse: Codable, Sendable {
    public let groups: [EngineeringValkeyCacheGroup]
}

public enum EngineeringValkeyBloomFilterTarget: String, Codable, CaseIterable, Sendable {
    case urlBlocklist = "url-blocklist"
    case emailBlocklist = "email-blocklist"
    case embedding
    case entityCache = "entity-cache"
    case apiKeys = "api-keys"

    public static let webVisibleCases: [Self] = [.urlBlocklist, .emailBlocklist, .embedding, .entityCache]

    public var displayName: String {
        switch self {
        case .urlBlocklist:
            "URL blocklist"
        case .emailBlocklist:
            "Email blocklist"
        case .embedding:
            "Embedding"
        case .entityCache:
            "Entity cache"
        case .apiKeys:
            "API keys"
        }
    }
}

public struct EngineeringValkeyRebuildResponse: Codable, Sendable {
    public let success: Bool
    public let filter: String
}

public struct EngineeringValkeyClearCacheResponse: Codable, Sendable {
    public let success: Bool
    public let group: String
}

public enum EngineeringValkeyFlushConcern: String, Codable, CaseIterable, Sendable {
    case caches
    case recentlyViewed = "recently-viewed"
    case blooms
    case rateLimiter = "rate-limiter"
    case dynamicConfig = "dynamic-config"
    case sessions
    case queues

    public var displayName: String {
        switch self {
        case .caches:
            "Caches"
        case .recentlyViewed:
            "Recently Viewed"
        case .blooms:
            "Bloom Filters"
        case .rateLimiter:
            "Rate Limiter"
        case .dynamicConfig:
            "Dynamic Config"
        case .sessions:
            "Sessions"
        case .queues:
            "Queues"
        }
    }

    /// Flushing forcibly logs out every user and invalidates in-flight passkey/MFA/OAuth challenges.
    public var requiresForce: Bool {
        self == .sessions
    }
}

/// `keysRemoved` is sent camelCase-as-is by the route (no snake_case transform on this response);
/// see `ArticleSyncTriggerResponse.jobId` above for the same wire behavior.
public struct EngineeringValkeyFlushResponse: Codable, Sendable {
    public let concern: String
    public let keysRemoved: Int?
}
