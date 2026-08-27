import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EngineeringOpsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testQueuesViewModelLoadsAndPausesQueue() async throws {
        stubQueuesLoad(paused: false)
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.stats?.queueCount, 5)
        XCTAssertEqual(viewModel.queues.first?.name, "emails")

        CannedFeedURLProtocol.queuedHandlers["/api/v1/mq/queues"] = [
            (queuesData(paused: true), 200, 0)
        ]
        await viewModel.pauseQueue(name: "emails")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/mq/queues/emails/pause" })
        XCTAssertEqual(viewModel.queues.first?.paused, Optional(true))
    }

    func testQueuesViewModelRunsRemainingActionsAndReportsFailures() async throws {
        stubQueuesLoad(paused: false)
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.resumeQueue(name: "emails")
        await viewModel.runScheduledJob(id: "daily-email")
        await viewModel.runBackfill(id: "emails-backfill")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/mq/queues/emails/resume"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/mq/scheduled-jobs/daily-email/runs"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/mq/backfills/emails-backfill/runs"
        })

        CannedFeedURLProtocol.handlers["/api/v1/mq/queues/emails/pause"] = (Data(#"{"error":"offline"}"#.utf8), 500)
        await viewModel.pauseQueue(name: "emails")

        XCTAssertNotNil(viewModel.actionErrorMessage)
    }

    func testPostgresqlViewModelLoadsAndTriggersArticleSync() async throws {
        stubPostgresqlLoad()
        CannedFeedURLProtocol.handlers["/api/v1/article-syncs"] = (Data(#"{"jobId":"job-1"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/article-syncs/job-1"] = (
            Data(#"{"status":"active"}"#.utf8),
            200
        )
        let viewModel = try NativeEngineeringPostgresqlViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.triggerArticleSync()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/article-syncs" })
        XCTAssertEqual(viewModel.articleSyncJobId, "job-1")
        if case .active = viewModel.articleSyncStatus {
            XCTAssertTrue(true)
        } else {
            XCTFail("Expected active article sync status")
        }
    }

    func testPostgresqlViewModelRunsJobsPendingActionsAndStatusVariants() async throws {
        stubPostgresqlLoad()
        CannedFeedURLProtocol.handlers["/api/v1/article-syncs/job-2"] = (
            Data(#"{"status":"failed","error":"bad feed"}"#.utf8),
            200
        )
        let viewModel = try NativeEngineeringPostgresqlViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.runJob(.runViews)
        viewModel.pendingAction = .createPartitions
        await viewModel.confirmPendingAction()
        viewModel.articleSyncJobId = "job-2"
        await viewModel.refreshArticleSyncStatus()

        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""type":"runViews""#)
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self).contains {
            $0.contains(#""type":"createPartitions""#)
        })
        if case let .failed(error) = viewModel.articleSyncStatus {
            XCTAssertEqual(error, "bad feed")
        } else {
            XCTFail("Expected failed article sync status")
        }
    }

    func testValkeyViewModelLoadsAndClearsCacheGroup() async throws {
        stubValkeyLoad()
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())

        await viewModel.load()
        XCTAssertEqual(viewModel.cacheGroups.map(\.name), ["users"])

        await viewModel.clearCache(group: "users")

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/valkey/caches/clear" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
            .contains { $0.contains(#""group":"users""#) })
    }

    func testValkeyViewModelRebuildsAndConfirmsPendingActions() async throws {
        stubValkeyLoad()
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.rebuild(filter: .entityCache)
        viewModel.pendingAction = .clearAll
        await viewModel.confirmPendingAction()
        viewModel.pendingAction = .clearGroup("users")
        await viewModel.confirmPendingAction()

        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
            .contains { $0.contains(#""filter":"entity-cache""#) })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
            .contains { $0.contains(#""group":"all""#) })
        XCTAssertNil(viewModel.pendingAction)
    }

    func testValkeyBloomFilterControlsMatchWebVisibleTargets() {
        XCTAssertEqual(
            EngineeringValkeyBloomFilterTarget.webVisibleCases,
            [.urlBlocklist, .emailBlocklist, .embedding, .entityCache]
        )
        XCTAssertFalse(EngineeringValkeyBloomFilterTarget.webVisibleCases.contains(.apiKeys))
    }

    func testRouteSurfaceRendersEngineeringOpsSurfaces() throws {
        let client = try makeClient()
        stubQueuesLoad(paused: false)
        stubPostgresqlLoad()
        stubValkeyLoad()

        let queues = try NativeRouteDestinationSurface(
            entry: entry(for: .engineeringQueues),
            client: client,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertNoThrow(try queues.inspect().find(NativeEngineeringQueuesSurface.self))

        let postgresql = try NativeRouteDestinationSurface(
            entry: entry(for: .engineeringPostgresql),
            client: client,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertNoThrow(try postgresql.inspect().find(NativeEngineeringPostgresqlSurface.self))

        let valkey = try NativeRouteDestinationSurface(
            entry: entry(for: .engineeringValkey),
            client: client,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        XCTAssertNoThrow(try valkey.inspect().find(NativeEngineeringValkeySurface.self))
    }

    func testEngineeringRouteDestinationHelpersSkipRemoteLoadingAndKeepClient() throws {
        let client = try makeClient()
        let queueRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/queues"))
        let postgresqlRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/postgresql"))
        let valkeyRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/valkey"))

        let queueSurface = NativeRouteDestinationSurface(
            entry: queueRoute.entry,
            client: client,
            routeMatch: queueRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let postgresqlSurface = NativeRouteDestinationSurface(
            entry: postgresqlRoute.entry,
            client: client,
            routeMatch: postgresqlRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let valkeySurface = NativeRouteDestinationSurface(
            entry: valkeyRoute.entry,
            client: client,
            routeMatch: valkeyRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )

        XCTAssertFalse(queueSurface.shouldLoadRouteSurfaceContent)
        XCTAssertFalse(postgresqlSurface.shouldLoadRouteSurfaceContent)
        XCTAssertFalse(valkeySurface.shouldLoadRouteSurfaceContent)
    }

    func testEngineeringRouteDestinationSurfacesShowSignInFallbackWithoutClient() throws {
        let queueRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/queues"))
        let postgresqlRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/postgresql"))
        let valkeyRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/admin/valkey"))

        XCTAssertNoThrow(try NativeRouteDestinationSurface(
            entry: queueRoute.entry,
            client: nil,
            routeMatch: queueRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeRouteDestinationSurface(
            entry: postgresqlRoute.entry,
            client: nil,
            routeMatch: postgresqlRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeRouteDestinationSurface(
            entry: valkeyRoute.entry,
            client: nil,
            routeMatch: valkeyRoute.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        ).inspect().find(text: "Sign in required"))
    }

    func testEngineeringSurfaceHelpersCoverFormattingAndActionMessages() throws {
        XCTAssertEqual(
            engineeringActionErrorMessage(for: VouchaError.unauthorized),
            .verbatim("Sign in to continue.")
        )
        XCTAssertEqual(
            engineeringActionErrorMessage(for: NSError(
                domain: "test",
                code: 1,
                userInfo: [NSLocalizedDescriptionKey: "Boom"]
            )),
            .verbatim("Boom")
        )

        let queuesSurface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        XCTAssertEqual(
            uiEnglish(queuesSurface.queueStatsDetail(
                EngineeringAggregatedQueueStats(
                    totalWaiting: 1,
                    totalActive: 2,
                    totalCompleted: 3,
                    totalFailed: 4,
                    queueCount: 5
                )
            )),
            "1 waiting, 2 active, 4 failed, 5 queues"
        )
        XCTAssertEqual(
            queuesSurface.queueActionKey(
                EngineeringQueueStats(
                    name: "emails",
                    waiting: 1,
                    active: 0,
                    completed: 2,
                    failed: 0,
                    paused: false
                )
            ),
            "pause|emails"
        )
        XCTAssertEqual(
            queuesSurface.queueActionKey(
                EngineeringQueueStats(
                    name: "emails",
                    waiting: 1,
                    active: 0,
                    completed: 2,
                    failed: 0,
                    paused: true
                )
            ),
            "resume|emails"
        )

        let postgresqlSurface = try NativeEngineeringPostgresqlSurface(
            entry: entry(for: .engineeringPostgresql),
            client: nil
        )
        XCTAssertEqual(
            uiEnglish(postgresqlSurface.confirmMessage(for: .createPartitions)),
            "This will create future monthly partitions."
        )
        XCTAssertEqual(
            uiEnglish(postgresqlSurface.confirmMessage(for: .cleanupPartitions)),
            "This will drop expired monthly partitions."
        )
        XCTAssertNil(postgresqlSurface.confirmMessage(for: nil))
        XCTAssertEqual(uiEnglish(postgresqlSurface.confirmButtonTitle(for: .createPartitions)), "Create Partitions")
        XCTAssertEqual(uiEnglish(postgresqlSurface.confirmButtonTitle(for: .cleanupPartitions)), "Cleanup Partitions")
        XCTAssertEqual(uiEnglish(postgresqlSurface.confirmButtonTitle(for: nil)), "Confirm")
        XCTAssertEqual(uiEnglish(postgresqlSurface.title(for: .runMigrations)), "Run Migrations")
        XCTAssertEqual(uiEnglish(postgresqlSurface.title(for: .runConfigDriven)), "Run Config-Driven")
        XCTAssertEqual(
            uiEnglish(postgresqlSurface.detail(for: .runViews)),
            "Rebuild materialized views and derived views."
        )
        XCTAssertEqual(
            uiEnglish(postgresqlSurface.partitionDetail(
                EngineeringPartitionTable(
                    name: "posts",
                    partitionCount: 1,
                    totalSizeBytes: 123,
                    partitions: []
                )
            )),
            "1 partitions · 123 bytes"
        )

        let active = postgresqlSurface.articleSyncStatusView(.active)
        XCTAssertEqual(try active.inspect().text().string(), "Article sync is active.")

        let completed = postgresqlSurface.articleSyncStatusView(
            .completed(
                ArticleSyncResult(
                    results: [],
                    summary: ArticleSyncSummary(created: 1, updated: 2, skipped: 3, errored: 4)
                )
            )
        )
        XCTAssertEqual(
            try completed.inspect().text().string(),
            "Completed. 1 created, 2 updated, 3 skipped, 4 errors"
        )

        let failed = postgresqlSurface.articleSyncStatusView(.failed("boom"))
        XCTAssertEqual(try failed.inspect().text().string(), "Failed. boom")

        let valkeySurface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)
        XCTAssertEqual(
            uiEnglish(valkeySurface.confirmMessage(for: .clearGroup("users"))),
            "This will clear the users cache group."
        )
        XCTAssertEqual(uiEnglish(valkeySurface.confirmMessage(for: .clearAll)), "This will clear every cache group.")
        XCTAssertNil(valkeySurface.confirmMessage(for: nil))
    }

    private func stubQueuesLoad(paused: Bool) {
        CannedFeedURLProtocol.handlers["/api/v1/mq/stats"] = (statsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues"] = (queuesData(paused: paused), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/scheduled-jobs"] = (scheduledJobsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/backfills"] = (backfillsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues/emails/pause"] = (Data(#"{"success":true}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/queues/emails/resume"] = (Data(#"{"success":true}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/mq/scheduled-jobs/daily-email/runs"] = (
            Data(#"{"success":true}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/mq/backfills/emails-backfill/runs"] = (
            Data(#"{"success":true}"#.utf8),
            200
        )
    }

    private func stubPostgresqlLoad() {
        CannedFeedURLProtocol.handlers["/api/v1/psql/migrations"] = (migrationsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/psql/partitions"] = (partitionsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/psql/jobs"] = (Data(#"{"success":true}"#.utf8), 200)
    }

    private func stubValkeyLoad() {
        CannedFeedURLProtocol.handlers["/api/v1/valkey/cache-groups"] = (cacheGroupsData(), 200)
        CannedFeedURLProtocol.handlers["/api/v1/valkey/bloom-filters/rebuild"] = (
            Data(#"{"success":true,"filter":"entity-cache"}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/valkey/caches/clear"] = (
            Data(#"{"success":true,"group":"users"}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/valkey/flush"] = (
            Data(#"{"concern":"blooms","keysRemoved":12}"#.utf8),
            200
        )
    }

    private func statsData() -> Data {
        Data(
            #"{"stats":{"totalWaiting":1,"totalActive":2,"totalCompleted":3,"totalFailed":4,"queueCount":5}}"#
                .utf8
        )
    }

    private func queuesData(paused: Bool) -> Data {
        Data(
            #"{"queues":[{"name":"emails","waiting":1,"active":0,"completed":12,"failed":0,"paused":\#(paused)}],"total":1}"#
                .utf8
        )
    }

    private func scheduledJobsData() -> Data {
        Data(
            #"{"jobs":[{"id":"daily-email","queue_name":"emails","job_name":"sendDailyEmail","schedule":"0 0 * * *","description":"Send daily email"}]}"#
                .utf8
        )
    }

    private func backfillsData() -> Data {
        Data(
            #"{"backfills":[{"id":"emails-backfill","queue_name":"emails","job_name":"backfillEmails","description":"Backfill emails","source_table":"posts"}]}"#
                .utf8
        )
    }

    private func migrationsData() -> Data {
        Data(#"{"applied":["001.sql"],"pending":["002.sql"],"total":2}"#.utf8)
    }

    private func partitionsData() -> Data {
        Data(
            #"{"tables":[{"name":"posts","partition_count":1,"total_size_bytes":123,"partitions":[{"name":"posts_2026_07","size_bytes":123}]}]}"#
                .utf8
        )
    }

    private func cacheGroupsData() -> Data {
        Data(#"{"groups":[{"name":"users","prefixes":["users_private","users_public"]}]}"#.utf8)
    }
}
