private struct EngineeringPsqlJobBody: Encodable {
    let type: EngineeringPsqlJobType
}

private struct EngineeringValkeyRebuildBody: Encodable {
    let filter: EngineeringValkeyBloomFilterTarget
}

private struct EngineeringValkeyClearCacheBody: Encodable {
    let group: String
}

private struct EngineeringValkeyFlushBody: Encodable {
    let concern: EngineeringValkeyFlushConcern
    let force: Bool?
}

public extension Endpoint {
    static var mqStats: Endpoint {
        Endpoint(.GET, path: "/api/v1/mq/stats")
    }

    static var mqQueues: Endpoint {
        Endpoint(.GET, path: "/api/v1/mq/queues")
    }

    static var mqScheduledJobs: Endpoint {
        Endpoint(.GET, path: "/api/v1/mq/scheduled-jobs")
    }

    static var mqBackfills: Endpoint {
        Endpoint(.GET, path: "/api/v1/mq/backfills")
    }

    static func mqPauseQueue(name: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/mq/queues/\(pathSegment(name))/pause")
    }

    static func mqResumeQueue(name: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/mq/queues/\(pathSegment(name))/resume")
    }

    static func mqRunScheduledJob(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/mq/scheduled-jobs/\(pathSegment(id))/runs")
    }

    static func mqRunBackfill(id: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/mq/backfills/\(pathSegment(id))/runs")
    }

    static var psqlMigrations: Endpoint {
        Endpoint(.GET, path: "/api/v1/psql/migrations")
    }

    static var psqlPartitions: Endpoint {
        Endpoint(.GET, path: "/api/v1/psql/partitions")
    }

    static func psqlJobs(type: EngineeringPsqlJobType) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/psql/jobs", body: EngineeringPsqlJobBody(type: type))
    }

    static func articleSyncStatus(jobId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/article-syncs/\(pathSegment(jobId))")
    }

    static var articleSyncTrigger: Endpoint {
        Endpoint(.POST, path: "/api/v1/article-syncs")
    }

    static var valkeyCacheGroups: Endpoint {
        Endpoint(.GET, path: "/api/v1/valkey/cache-groups")
    }

    static func valkeyRebuildBloomFilter(filter: EngineeringValkeyBloomFilterTarget) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/valkey/bloom-filters/rebuild",
            body: EngineeringValkeyRebuildBody(filter: filter)
        )
    }

    static func valkeyClearCache(group: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/valkey/caches/clear", body: EngineeringValkeyClearCacheBody(group: group))
    }

    static func valkeyFlush(concern: EngineeringValkeyFlushConcern, force: Bool? = nil) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/valkey/flush", body: EngineeringValkeyFlushBody(concern: concern, force: force))
    }
}
