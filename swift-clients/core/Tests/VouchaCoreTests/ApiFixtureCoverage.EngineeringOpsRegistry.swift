import VouchaAPI

let engineeringOpsApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.admin-ai-costs.default") {
        try assertFixtureCoversDTO($0, as: AiCostTotalsPage.self)
    },
    RegisteredFixture(id: "native.admin-ai-costs.page-2") {
        try assertFixtureCoversDTO($0, as: AiCostTotalsPage.self)
    },
    RegisteredFixture(id: "native.admin-ai-costs.empty") {
        try assertFixtureCoversDTO($0, as: AiCostTotalsPage.self)
    },
    RegisteredFixture(id: "web.admin.article-syncs.trigger.default") {
        try assertFixtureCoversDTO($0, as: ArticleSyncTriggerResponse.self, ignoring: ["jobId"])
    },
    RegisteredFixture(id: "web.admin.article-syncs.status.active") {
        try assertFixtureCoversDTO($0, as: ArticleSyncJobStatus.self)
    },
    RegisteredFixture(id: "web.admin.mq.stats.default") {
        try assertFixtureCoversDTO($0, as: EngineeringQueueStatsSummaryResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.queues.default") {
        try assertFixtureCoversDTO($0, as: EngineeringQueueStatsResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.queues.pause.default") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.queues.resume.default") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.scheduled-jobs.default") {
        try assertFixtureCoversDTO($0, as: EngineeringScheduledJobsResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.scheduled-jobs.trigger.default") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.backfills.default") {
        try assertFixtureCoversDTO($0, as: EngineeringBackfillsResponse.self)
    },
    RegisteredFixture(id: "web.admin.mq.backfills.trigger.default") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    },
    RegisteredFixture(id: "web.admin.psql.migrations.default") {
        try assertFixtureCoversDTO($0, as: EngineeringMigrationsResponse.self)
    },
    RegisteredFixture(id: "web.admin.psql.partitions.default") {
        try assertFixtureCoversDTO($0, as: EngineeringPartitionStatusResponse.self)
    },
    RegisteredFixture(id: "web.admin.psql.jobs.default") {
        try assertFixtureCoversDTO($0, as: EngineeringSuccessResponse.self)
    },
    RegisteredFixture(id: "web.admin.valkey.bloom-filters.rebuild.default") {
        try assertFixtureCoversDTO($0, as: EngineeringValkeyRebuildResponse.self)
    },
    RegisteredFixture(id: "web.admin.valkey.cache-groups.default") {
        try assertFixtureCoversDTO($0, as: EngineeringValkeyCacheGroupsResponse.self)
    },
    RegisteredFixture(id: "web.admin.valkey.caches.clear.default") {
        try assertFixtureCoversDTO($0, as: EngineeringValkeyClearCacheResponse.self)
    },
    RegisteredFixture(id: "web.admin.valkey.flush.default") {
        try assertFixtureCoversDTO($0, as: EngineeringValkeyFlushResponse.self, ignoring: ["keysRemoved"])
    }
]
