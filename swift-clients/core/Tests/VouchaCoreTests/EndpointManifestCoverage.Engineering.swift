@testable import VouchaAPI

extension EndpointManifestCoverage {
    static let engineeringEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.admin-ai-costs.default") {
            ApiFixtureEndpointCoverageTests.registry["native.admin-ai-costs.default"]!
        },
        ManifestRegisteredEndpoint(id: "native.admin-ai-costs.page-2") {
            ApiFixtureEndpointCoverageTests.registry["native.admin-ai-costs.page-2"]!
        },
        ManifestRegisteredEndpoint(id: "native.admin-ai-costs.empty") {
            ApiFixtureEndpointCoverageTests.registry["native.admin-ai-costs.empty"]!
        },
        ManifestRegisteredEndpoint(id: "web.admin.article-syncs.trigger.default") {
            Endpoint.articleSyncTrigger
        },
        ManifestRegisteredEndpoint(id: "web.admin.article-syncs.status.active") {
            Endpoint.articleSyncStatus(jobId: "job-1")
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.stats.default") {
            Endpoint.mqStats
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.queues.default") {
            Endpoint.mqQueues
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.queues.pause.default") {
            Endpoint.mqPauseQueue(name: "psql")
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.queues.resume.default") {
            Endpoint.mqResumeQueue(name: "psql")
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.scheduled-jobs.default") {
            Endpoint.mqScheduledJobs
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.scheduled-jobs.trigger.default") {
            Endpoint.mqRunScheduledJob(id: "kagi-smallweb-sync")
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.backfills.default") {
            Endpoint.mqBackfills
        },
        ManifestRegisteredEndpoint(id: "web.admin.mq.backfills.trigger.default") {
            Endpoint.mqRunBackfill(id: "openai-moderation-posts")
        },
        ManifestRegisteredEndpoint(id: "web.admin.psql.migrations.default") {
            Endpoint.psqlMigrations
        },
        ManifestRegisteredEndpoint(id: "web.admin.psql.partitions.default") {
            Endpoint.psqlPartitions
        },
        ManifestRegisteredEndpoint(id: "web.admin.psql.jobs.default") {
            Endpoint.psqlJobs(type: .runConfigDriven)
        },
        ManifestRegisteredEndpoint(id: "web.admin.valkey.bloom-filters.rebuild.default") {
            Endpoint.valkeyRebuildBloomFilter(filter: .entityCache)
        },
        ManifestRegisteredEndpoint(id: "web.admin.valkey.cache-groups.default") {
            Endpoint.valkeyCacheGroups
        },
        ManifestRegisteredEndpoint(id: "web.admin.valkey.caches.clear.default") {
            Endpoint.valkeyClearCache(group: "posts")
        },
        ManifestRegisteredEndpoint(id: "web.admin.valkey.flush.default") {
            Endpoint.valkeyFlush(concern: .blooms)
        },
        ManifestRegisteredEndpoint(id: "native.feature-flags.default") {
            Endpoint.featureFlags
        },
        ManifestRegisteredEndpoint(id: "native.captcha-config.default") {
            Endpoint.captchaConfig
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.namespaces.developer") {
            Endpoint.dynamicConfigNamespaces
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.namespace.typed") {
            Endpoint.dynamicConfigNamespace("recaptcha-config")
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.namespace.string") {
            Endpoint.dynamicConfigNamespace("app-attestation-config")
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.namespace.integer") {
            Endpoint.dynamicConfigNamespace("post-content-limits-config")
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.update.changed") {
            Endpoint.updateDynamicConfigNamespace("feature-flags", field: "fediverse", value: .boolean(true))
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.update.no-op") {
            Endpoint.updateDynamicConfigNamespace("feature-flags", field: "fediverse", value: .boolean(false))
        },
        ManifestRegisteredEndpoint(id: "native.dynamic-config.history.default") {
            Endpoint.dynamicConfigHistory("feature-flags")
        }
    ]
}
