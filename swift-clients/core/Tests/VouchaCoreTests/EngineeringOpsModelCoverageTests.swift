import Foundation
@testable import VouchaAPI
import XCTest

final class EngineeringOpsModelCoverageTests: XCTestCase {
    func testEngineeringQueueAndJobModelsDecodeSnakeCaseAndExposeStableIdentifiers() throws {
        let decoder = makeVouchaDecoder()

        let queueStats = try decoder.decode(EngineeringQueueStatsResponse.self, from: Data("""
        {
          "queues": [
            {
              "name": "emails",
              "waiting": 1,
              "active": 2,
              "completed": 3,
              "failed": 4,
              "delayed": 0,
              "paused": false
            }
          ],
          "total": 1
        }
        """.utf8))
        XCTAssertEqual(queueStats.queues.first?.name, "emails")
        XCTAssertEqual(queueStats.queues.first?.waiting, 1)

        let scheduledJobs = try decoder.decode(EngineeringScheduledJobsResponse.self, from: Data("""
        {
          "jobs": [
            {
              "id": "daily-email",
              "queue_name": "emails",
              "job_name": "sendDailyEmail",
              "schedule": "0 0 * * *",
              "description": "Send daily email"
            }
          ]
        }
        """.utf8))
        XCTAssertEqual(scheduledJobs.jobs.first?.id, "daily-email")
        XCTAssertEqual(scheduledJobs.jobs.first?.queueName, "emails")

        let backfills = try decoder.decode(EngineeringBackfillsResponse.self, from: Data("""
        {
          "backfills": [
            {
              "id": "emails-backfill",
              "queue_name": "emails",
              "job_name": "backfillEmails",
              "description": "Backfill emails",
              "source_table": "posts"
            }
          ]
        }
        """.utf8))
        XCTAssertEqual(backfills.backfills.first?.id, "emails-backfill")
        XCTAssertEqual(backfills.backfills.first?.sourceTable, "posts")

        let partitions = try decoder.decode(EngineeringPartitionStatusResponse.self, from: Data("""
        {
          "tables": [
            {
              "name": "posts",
              "partition_count": 2,
              "total_size_bytes": 123,
              "partitions": [
                { "name": "posts_2026_07", "size_bytes": 100 }
              ]
            }
          ]
        }
        """.utf8))
        XCTAssertEqual(partitions.tables.first?.id, "posts")
        XCTAssertEqual(partitions.tables.first?.partitions.first?.id, "posts_2026_07")

        let cacheGroups = try decoder.decode(EngineeringValkeyCacheGroupsResponse.self, from: Data("""
        {
          "groups": [
            {
              "name": "users",
              "prefixes": ["users_private", "users_public"]
            }
          ]
        }
        """.utf8))
        XCTAssertEqual(cacheGroups.groups.first?.id, "users")
        XCTAssertEqual(cacheGroups.groups.first?.prefixes, ["users_private", "users_public"])
    }

    func testEngineeringOpsCustomEncodingsMatchBackendShape() throws {
        try assertEncoded(
            EngineeringAggregatedQueueStats(
                totalWaiting: 1,
                totalActive: 2,
                totalCompleted: 3,
                totalFailed: 4,
                totalDelayed: 0,
                queueCount: 5
            ),
            equals: [
                "queueCount": 5,
                "totalActive": 2,
                "totalCompleted": 3,
                "totalFailed": 4,
                "totalDelayed": 0,
                "totalWaiting": 1
            ]
        )

        try assertEncoded(
            ArticleSyncTriggerResponse(jobId: "job-1"),
            equals: [
                "job_id": "job-1"
            ]
        )

        try assertEncoded(
            ArticleSyncJobStatus.completed(
                ArticleSyncResult(
                    results: [],
                    summary: ArticleSyncSummary(created: 1, updated: 2, skipped: 3, errored: 4)
                )
            ),
            equals: [
                "result": [
                    "results": [],
                    "summary": [
                        "created": 1,
                        "errored": 4,
                        "skipped": 3,
                        "updated": 2
                    ]
                ],
                "status": "completed"
            ]
        )

        try assertEncoded(
            ArticleSyncJobStatus.failed("boom"),
            equals: [
                "error": "boom",
                "status": "failed"
            ]
        )
    }

    func testValkeyBloomFilterTargetsExposeDisplayNamesAndVisibility() {
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.urlBlocklist.displayName, "URL blocklist")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.emailBlocklist.displayName, "Email blocklist")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.embedding.displayName, "Embedding")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.entityCache.displayName, "Entity cache")
        XCTAssertEqual(EngineeringValkeyBloomFilterTarget.apiKeys.displayName, "API keys")
        XCTAssertEqual(
            EngineeringValkeyBloomFilterTarget.webVisibleCases,
            [.urlBlocklist, .emailBlocklist, .embedding, .entityCache]
        )
    }

    func testValkeyFlushConcernsExposeDisplayNames() {
        XCTAssertEqual(EngineeringValkeyFlushConcern.caches.displayName, "Caches")
        XCTAssertEqual(EngineeringValkeyFlushConcern.recentlyViewed.displayName, "Recently Viewed")
        XCTAssertEqual(EngineeringValkeyFlushConcern.blooms.displayName, "Bloom Filters")
        XCTAssertEqual(EngineeringValkeyFlushConcern.rateLimiter.displayName, "Rate Limiter")
        XCTAssertEqual(EngineeringValkeyFlushConcern.dynamicConfig.displayName, "Dynamic Config")
        XCTAssertEqual(EngineeringValkeyFlushConcern.sessions.displayName, "Sessions")
        XCTAssertEqual(EngineeringValkeyFlushConcern.queues.displayName, "Queues")
    }

    private func assertEncoded(
        _ value: some Encodable,
        equals expected: [String: Any],
        file: StaticString = #filePath,
        line: UInt = #line
    ) throws {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        encoder.outputFormatting = [.sortedKeys]
        let data = try encoder.encode(value)
        let object = try JSONSerialization.jsonObject(with: data)
        XCTAssertTrue(matches(object, expected), file: file, line: line)
    }

    private func matches(_ lhs: Any, _ rhs: Any) -> Bool {
        switch (lhs, rhs) {
        case let (lhs as [String: Any], rhs as [String: Any]):
            guard lhs.count == rhs.count else { return false }
            return rhs.allSatisfy { key, value in
                guard let lhsValue = lhs[key] else { return false }
                return matches(lhsValue, value)
            }
        case let (lhs as [Any], rhs as [Any]):
            guard lhs.count == rhs.count else { return false }
            return zip(lhs, rhs).allSatisfy(matches)
        case let (lhs as String, rhs as String):
            return lhs == rhs
        case let (lhs as Int, rhs as Int):
            return lhs == rhs
        case let (lhs as Bool, rhs as Bool):
            return lhs == rhs
        case (_ as NSNull, _ as NSNull):
            return true
        default:
            return false
        }
    }
}
