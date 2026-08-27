import Foundation
@testable import VouchaAPI
import XCTest

final class EngineeringOpsEndpointTests: XCTestCase {
    func testMqRouteHelpersUseExpectedPaths() {
        assertEndpoint(.mqStats, path: "/api/v1/mq/stats")
        assertEndpoint(.mqQueues, path: "/api/v1/mq/queues")
        assertEndpoint(.mqScheduledJobs, path: "/api/v1/mq/scheduled-jobs")
        assertEndpoint(.mqBackfills, path: "/api/v1/mq/backfills")
        assertEndpoint(.mqPauseQueue(name: "emails"), method: .POST, path: "/api/v1/mq/queues/emails/pause")
        assertEndpoint(.mqResumeQueue(name: "emails"), method: .POST, path: "/api/v1/mq/queues/emails/resume")
        assertEndpoint(
            .mqRunScheduledJob(id: "job 1"),
            method: .POST,
            path: "/api/v1/mq/scheduled-jobs/job%201/runs"
        )
        assertEndpoint(.mqRunBackfill(id: "backfill 1"), method: .POST, path: "/api/v1/mq/backfills/backfill%201/runs")
    }

    func testPsqlRouteHelpersUseExpectedPathsAndBodies() {
        assertEndpoint(.psqlMigrations, path: "/api/v1/psql/migrations")
        assertEndpoint(.psqlPartitions, path: "/api/v1/psql/partitions")
        assertEndpoint(
            .psqlJobs(type: .runMigrations),
            method: .POST,
            path: "/api/v1/psql/jobs",
            body: ["type": "runMigrations"]
        )
        assertEndpoint(
            .articleSyncTrigger,
            method: .POST,
            path: "/api/v1/article-syncs"
        )
        assertEndpoint(
            .articleSyncStatus(jobId: "job 1"),
            path: "/api/v1/article-syncs/job%201"
        )
    }

    func testPsqlPartitionAndArticleSyncModelsDecode() throws {
        let decoder = makeVouchaDecoder()
        let partitions = try decoder.decode(EngineeringPartitionStatusResponse.self, from: Data("""
        {
          "tables": [{
            "name": "posts",
            "partition_count": 2,
            "total_size_bytes": 123,
            "partitions": [{"name": "posts_2026_07", "size_bytes": 100}]
          }]
        }
        """.utf8))
        XCTAssertEqual(partitions.tables.first?.partitionCount, 2)

        let status = try decoder.decode(
            ArticleSyncJobStatus.self,
            from: Data(
                #"{"status":"completed","result":{"results":[],"summary":{"created":1,"updated":2,"skipped":3,"errored":4}}}"#
                    .utf8
            )
        )
        if case let .completed(result) = status {
            XCTAssertEqual(result.summary.updated, 2)
        } else {
            XCTFail("Expected completed article sync status")
        }

        let failed = try decoder.decode(
            ArticleSyncJobStatus.self,
            from: Data(#"{"status":"failed","error":"bad feed"}"#.utf8)
        )
        if case let .failed(error) = failed {
            XCTAssertEqual(error, "bad feed")
        } else {
            XCTFail("Expected failed article sync status")
        }
    }

    func testValkeyRouteHelpersUseExpectedPathsAndBodies() {
        assertEndpoint(.valkeyCacheGroups, path: "/api/v1/valkey/cache-groups")
        assertEndpoint(
            .valkeyRebuildBloomFilter(filter: .entityCache),
            method: .POST,
            path: "/api/v1/valkey/bloom-filters/rebuild",
            body: ["filter": "entity-cache"]
        )
        assertEndpoint(
            .valkeyClearCache(group: "all"),
            method: .POST,
            path: "/api/v1/valkey/caches/clear",
            body: ["group": "all"]
        )
        assertEndpoint(
            .valkeyFlush(concern: .blooms),
            method: .POST,
            path: "/api/v1/valkey/flush",
            body: ["concern": "blooms"]
        )
        assertEndpoint(
            .valkeyFlush(concern: .sessions, force: true),
            method: .POST,
            path: "/api/v1/valkey/flush",
            body: ["concern": "sessions", "force": true]
        )
    }

    func testValkeyResponseModelsDecodeSnakeCase() throws {
        let decoder = makeVouchaDecoder()
        let response = try decoder.decode(EngineeringValkeyCacheGroupsResponse.self, from: Data("""
        { "groups": [{ "name": "users", "prefixes": ["users_private", "users_public"] }] }
        """.utf8))
        XCTAssertEqual(response.groups.first?.prefixes, ["users_private", "users_public"])
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.urlBlocklist.displayName, "URL blocklist")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.emailBlocklist.displayName, "Email blocklist")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.embedding.displayName, "Embedding")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.entityCache.displayName, "Entity cache")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.apiKeys.displayName, "API keys")
    }

    func testValkeyFlushConcernRawValuesMatchBackendOrder() {
        XCTAssertEqual(
            EngineeringValkeyFlushConcern.allCases.map(\.rawValue),
            ["caches", "recently-viewed", "blooms", "rate-limiter", "dynamic-config", "sessions", "queues"]
        )
        XCTAssertTrue(EngineeringValkeyFlushConcern.sessions.requiresForce)
        XCTAssertFalse(EngineeringValkeyFlushConcern.blooms.requiresForce)
    }

    func testValkeyFlushResponseDecodesCamelCaseKeysRemoved() throws {
        let decoder = makeVouchaDecoder()
        let withCount = try decoder.decode(
            EngineeringValkeyFlushResponse.self,
            from: Data(#"{"concern":"blooms","keysRemoved":12}"#.utf8)
        )
        XCTAssertEqual(withCount.concern, "blooms")
        XCTAssertEqual(withCount.keysRemoved, 12)

        let withoutCount = try decoder.decode(
            EngineeringValkeyFlushResponse.self,
            from: Data(#"{"concern":"caches","keysRemoved":null}"#.utf8)
        )
        XCTAssertNil(withoutCount.keysRemoved)
    }
}
