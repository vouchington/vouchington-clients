import SwiftUI
import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class EngineeringOpsSurfaceCoverageTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    func testQueuesSurfaceLoadedContentRendersOperationalSections() throws {
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        viewModel.state = .loaded
        viewModel.stats = EngineeringAggregatedQueueStats(
            totalWaiting: 1,
            totalActive: 2,
            totalCompleted: 3,
            totalFailed: 4,
            queueCount: 5
        )
        viewModel.queues = [
            EngineeringQueueStats(name: "emails", waiting: 1, active: 0, completed: 12, failed: 0, paused: false),
            EngineeringQueueStats(name: "imports", waiting: 0, active: 1, completed: 9, failed: 2, paused: true)
        ]
        viewModel.scheduledJobs = [
            EngineeringScheduledJob(
                id: "daily-email",
                queueName: "emails",
                jobName: "sendDailyEmail",
                schedule: "0 0 * * *",
                description: "Send daily email"
            )
        ]
        viewModel.backfills = [
            EngineeringBackfill(
                id: "emails-backfill",
                queueName: "emails",
                jobName: "backfillEmails",
                description: "Backfill emails",
                sourceTable: "posts"
            )
        ]
        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let inspection = try surface.loadedContent(viewModel).inspect()

        XCTAssertEqual(try inspection.find(text: "Queue Stats").string(), "Queue Stats")
        XCTAssertEqual(try inspection.find(text: "Queues").string(), "Queues")
        XCTAssertEqual(try inspection.find(text: "Scheduled Jobs").string(), "Scheduled Jobs")
        XCTAssertEqual(try inspection.find(text: "Backfills").string(), "Backfills")
        XCTAssertEqual(try inspection.find(text: "emails").string(), "emails")
        XCTAssertEqual(try inspection.find(text: "daily-email").string(), "daily-email")
        XCTAssertEqual(try inspection.find(text: "emails-backfill").string(), "emails-backfill")
        XCTAssertNoThrow(try inspection.find(button: "Pause"))
        XCTAssertNoThrow(try inspection.find(button: "Resume"))
        XCTAssertNoThrow(try inspection.find(button: "Run"))
        XCTAssertNoThrow(try inspection.find(button: "Run Backfill"))
    }

    func testQueuesSurfaceLoadedContentRendersEmptyState() throws {
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        viewModel.state = .loaded

        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let inspection = try surface.loadedContent(viewModel).inspect()

        XCTAssertEqual(try inspection.find(text: "No queues").string(), "No queues")
        XCTAssertEqual(
            try inspection.find(text: "Queue monitoring data is not available yet.").string(),
            "Queue monitoring data is not available yet."
        )
    }

    func testEngineeringRouteDestinationContentUsesNativeListForCatalogEntries() throws {
        let client = try makeClient()
        let entry = try entry(for: .signIn)
        let surface = NativeRouteDestinationSurface(
            entry: entry,
            client: client,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )

        XCTAssertNoThrow(try surface.destinationContent.inspect().find(NativeListSurface.self))
    }

    func testEngineeringRoutesRequireSignedInClient() throws {
        let client = try makeClient()
        for destination in [
            NativeRouteDestinationIdentifier.engineeringQueues,
            .engineeringPostgresql,
            .engineeringValkey,
            .engineeringAiCosts
        ] {
            let entry = try entry(for: destination)

            XCTAssertNotNil(NativeRouteDestinationSurface.resolvedClient(
                entry: entry,
                client: client,
                isSignedIn: true
            ))
            XCTAssertNil(NativeRouteDestinationSurface.resolvedClient(
                entry: entry,
                client: client,
                isSignedIn: false
            ))
        }
    }

    func testAiCostsRouteUsesDedicatedSurfaceAndRejectsUnauthorizedViewersBeforeLoading() throws {
        let entry = try entry(for: .engineeringAiCosts)
        let client = try makeClient()
        let routeSurface = NativeRouteDestinationSurface(
            entry: entry,
            client: client,
            routeMatch: nil,
            routeQuery: nil,
            isSignedIn: true,
            isAdministrator: true,
            showSignIn: {}
        )

        XCTAssertNoThrow(try routeSurface.inspect().find(NativeAiCostsSurface.self))
        XCTAssertNoThrow(try NativeAiCostsSurface(
            entry: entry,
            client: nil,
            isAdministrator: true
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeAiCostsSurface(
            entry: entry,
            client: client,
            isAdministrator: false
        ).inspect().find(text: "Administrator required"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testEngineeringSurfaceBodiesRenderFallbackAndInitialContent() throws {
        let client = try makeClient()

        XCTAssertNoThrow(try NativeEngineeringQueuesSurface(
            entry: entry(for: .engineeringQueues),
            client: nil
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeEngineeringQueuesSurface(
            entry: entry(for: .engineeringQueues),
            client: client
        ).inspect().find(text: "No queues"))

        XCTAssertNoThrow(try NativeEngineeringValkeySurface(
            entry: entry(for: .engineeringValkey),
            client: nil
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeEngineeringValkeySurface(
            entry: entry(for: .engineeringValkey),
            client: client
        ).inspect().find(text: "Bloom Filter Rebuild"))
        XCTAssertNoThrow(try NativeEngineeringValkeySurface(
            entry: entry(for: .engineeringValkey),
            client: client
        ).inspect().find(text: "Flush Concerns"))

        XCTAssertNoThrow(try NativeEngineeringPostgresqlSurface(
            entry: entry(for: .engineeringPostgresql),
            client: nil
        ).inspect().find(text: "Sign in required"))
        XCTAssertNoThrow(try NativeEngineeringPostgresqlSurface(
            entry: entry(for: .engineeringPostgresql),
            client: client
        ).inspect().find(text: "Article Sync"))
    }

    func testEngineeringContentDismissesActionBindings() throws {
        let queuesViewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        queuesViewModel.state = .loaded
        queuesViewModel.actionErrorMessage = .verbatim("Queue failed")
        let queuesSurface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)

        try queuesSurface.content(queuesViewModel).inspect().alert().dismiss()

        XCTAssertNil(queuesViewModel.actionErrorMessage)

        let postgresqlViewModel = try NativeEngineeringPostgresqlViewModel(client: makeClient())
        postgresqlViewModel.state = .loaded
        postgresqlViewModel.actionErrorMessage = .verbatim("PostgreSQL failed")
        postgresqlViewModel.pendingAction = .cleanupPartitions
        let postgresqlSurface = try NativeEngineeringPostgresqlSurface(
            entry: entry(for: .engineeringPostgresql),
            client: nil
        )

        try postgresqlSurface.content(postgresqlViewModel).inspect().alert().dismiss()
        try postgresqlSurface.content(postgresqlViewModel).inspect().confirmationDialog().dismiss()

        XCTAssertNil(postgresqlViewModel.actionErrorMessage)
        XCTAssertNil(postgresqlViewModel.pendingAction)

        let valkeyViewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        valkeyViewModel.state = .loaded
        valkeyViewModel.pendingAction = .clearAll
        let valkeySurface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)

        try valkeySurface.content(valkeyViewModel).inspect().confirmationDialog().dismiss()

        XCTAssertNil(valkeyViewModel.pendingAction)
    }

    func testQueuesBackfillButtonStagesConfirmationBeforeDispatching() throws {
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        viewModel.state = .loaded
        viewModel.backfills = [
            EngineeringBackfill(
                id: "emails-backfill",
                queueName: "emails",
                jobName: "backfillEmails",
                description: "Backfill emails",
                sourceTable: "posts"
            )
        ]

        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let inspection = try surface.loadedContent(viewModel).inspect()

        try inspection.find(button: "Run Backfill").tap()

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/mq/backfills/emails-backfill/runs"
        })
    }

    func testValkeyRebuildButtonStagesConfirmationBeforeDispatching() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        viewModel.state = .loaded
        viewModel.cacheGroups = [
            EngineeringValkeyCacheGroup(name: "users", prefixes: ["users_private", "users_public"])
        ]

        let surface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)
        let inspection = try surface.rebuildSection(viewModel).inspect()

        try inspection.find(button: "Rebuild").tap()

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/valkey/bloom-filters/rebuild"
        })
    }

    func testValkeyFlushButtonStagesConfirmationBeforeDispatching() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        viewModel.state = .loaded

        let surface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)
        let inspection = try surface.flushSection(viewModel).inspect()

        try inspection.find(button: "Flush").tap()
        try inspection.find(button: "Force Flush").tap()

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/valkey/flush"
        })
    }

    func testValkeyFlushSessionsButtonIsDestructiveWhileOthersAreNot() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        viewModel.state = .loaded

        let surface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)
        let inspection = try surface.flushSection(viewModel).inspect()

        XCTAssertEqual(try inspection.find(button: "Force Flush").role(), .destructive)
        XCTAssertNil(try inspection.find(button: "Flush").role())
    }

    func testValkeyViewModelFlushesConcernsAndIncludesForceOnlyForSessions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/valkey/cache-groups"] = (
            Data(#"{"groups":[{"name":"users","prefixes":["users_private","users_public"]}]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/valkey/flush"] = (
            Data(#"{"concern":"blooms","keysRemoved":12}"#.utf8),
            200
        )
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.flush(concern: .blooms)
        await viewModel.flush(concern: .sessions)

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/valkey/flush" }.count,
            2
        )
        let bodies = CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
        XCTAssertTrue(bodies.contains {
            $0.contains(#""concern":"blooms""#) && !$0.contains("force")
        })
        XCTAssertTrue(bodies.contains {
            $0.contains(#""concern":"sessions""#) && $0.contains(#""force":true"#)
        })
    }

    func testEngineeringViewModelsReportLoadAndActionFailures() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/mq/stats"] = (Data(#"{"error":"offline"}"#.utf8), 500)
        let queuesViewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        await queuesViewModel.load()
        if case .error = queuesViewModel.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Expected queue load error")
        }

        CannedFeedURLProtocol.handlers["/api/v1/psql/migrations"] = (
            Data(#"{"applied":[],"pending":[],"total":0}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/psql/partitions"] = (
            Data(#"{"tables":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/psql/jobs"] = (Data(#"{"error":"offline"}"#.utf8), 500)
        let postgresqlViewModel = try NativeEngineeringPostgresqlViewModel(client: makeClient())
        await postgresqlViewModel.load()
        await postgresqlViewModel.runJob(.cleanupPartitions)
        XCTAssertNotNil(postgresqlViewModel.actionErrorMessage)

        CannedFeedURLProtocol.handlers["/api/v1/valkey/cache-groups"] = (Data(#"{"error":"offline"}"#.utf8), 500)
        let valkeyViewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        await valkeyViewModel.load()
        if case .error = valkeyViewModel.state {
            XCTAssertTrue(true)
        } else {
            XCTFail("Expected Valkey load error")
        }
    }

    private func flushConfirmationDialog(
        pendingConcern box: PendingFlushConcernBox,
        viewModel: NativeEngineeringValkeyViewModel
    ) throws -> InspectableView<ViewType.ConfirmationDialog> {
        let modifier = ValkeyFlushConfirmationModifier(
            pendingConcern: Binding(get: { box.value }, set: { box.value = $0 }),
            viewModel: viewModel
        )
        return try EmptyView().modifier(modifier).inspect().emptyView()
            .modifier(ValkeyFlushConfirmationModifier.self)
            .implicitAnyView().viewModifierContent().confirmationDialog()
    }

    func testValkeyFlushConfirmationModifierDispatchesSelectedConcern() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/valkey/flush"] = (
            Data(#"{"concern":"blooms","keysRemoved":3}"#.utf8),
            200
        )
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let box = PendingFlushConcernBox(.blooms)
        let dialog = try flushConfirmationDialog(pendingConcern: box, viewModel: viewModel)

        XCTAssertEqual(try dialog.title().string(), "Flush Bloom Filters?")
        try dialog.find(button: "Flush").tap()
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertNil(box.value)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/valkey/flush" })
        let bodies = CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
        XCTAssertTrue(bodies.contains {
            $0.contains(#""concern":"blooms""#) && !$0.contains("force")
        })
    }

    func testValkeyFlushConfirmationModifierDispatchesForceForSessions() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/valkey/flush"] = (
            Data(#"{"concern":"sessions","keysRemoved":9}"#.utf8),
            200
        )
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let box = PendingFlushConcernBox(.sessions)
        let dialog = try flushConfirmationDialog(pendingConcern: box, viewModel: viewModel)

        XCTAssertEqual(try dialog.title().string(), "Flush Sessions?")
        try dialog.find(button: "Force Logout Everyone").tap()
        try await Task.sleep(nanoseconds: 50_000_000)

        XCTAssertNil(box.value)
        let bodies = CannedFeedURLProtocol.capturedBodies.compactMap(\.self)
        XCTAssertTrue(bodies.contains {
            $0.contains(#""concern":"sessions""#) && $0.contains(#""force":true"#)
        })
    }

    func testValkeyFlushConfirmationModifierCancelClearsWithoutDispatching() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let box = PendingFlushConcernBox(.queues)
        let dialog = try flushConfirmationDialog(pendingConcern: box, viewModel: viewModel)

        try dialog.find(button: "Cancel").tap()

        XCTAssertNil(box.value)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/valkey/flush" })
    }

    func testValkeyFlushConfirmationModifierDismissClearsPendingConcern() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let box = PendingFlushConcernBox(.blooms)
        let dialog = try flushConfirmationDialog(pendingConcern: box, viewModel: viewModel)

        try dialog.dismiss()

        XCTAssertNil(box.value)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/valkey/flush" })
    }

    func testValkeyFlushConfirmationModifierMessageCoversAllConcerns() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let expectations: [(EngineeringValkeyFlushConcern, String, String)] = [
            (
                .caches,
                "Flush Caches?",
                "Clears all 6 entity cache groups (users, topics, posts, rss, urls, elections)."
            ),
            (.recentlyViewed, "Flush Recently Viewed?", "Clears all recently-viewed history entries."),
            (.blooms, "Flush Bloom Filters?", "Clears all bloom filter keys. Filters will need to be rebuilt."),
            (.rateLimiter, "Flush Rate Limiter?", "Resets all rate limiter state."),
            (.dynamicConfig, "Flush Dynamic Config?", "Clears all cached dynamic config values."),
            (
                .sessions,
                "Flush Sessions?",
                "Logs out every signed-in user and invalidates all in-flight passkey, MFA, " +
                    "and OAuth challenges. This cannot be undone."
            ),
            (.queues, "Flush Queues?", "Empties every queue. Enqueued jobs will be lost.")
        ]

        for (concern, expectedTitle, expectedMessage) in expectations {
            let box = PendingFlushConcernBox(concern)
            let dialog = try flushConfirmationDialog(pendingConcern: box, viewModel: viewModel)

            XCTAssertEqual(try dialog.title().string(), expectedTitle)
            XCTAssertEqual(try dialog.message().text().string(), expectedMessage)
            let confirmTitle = concern == .sessions ? "Force Logout Everyone" : "Flush"
            XCTAssertEqual(try dialog.find(button: confirmTitle).role(), .destructive)
        }
    }

    func testCacheSectionClearAllButtonStagesConfirmationBeforeDispatching() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        let surface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)

        try surface.cacheSection(viewModel).inspect().find(button: "Clear All").tap()

        XCTAssertEqual(viewModel.pendingAction, .clearAll)
    }

    func testCacheSectionGroupClearButtonStagesConfirmationBeforeDispatching() throws {
        let viewModel = try NativeEngineeringValkeyViewModel(client: makeClient())
        viewModel.cacheGroups = [
            EngineeringValkeyCacheGroup(name: "users", prefixes: ["user:", "user-session:"])
        ]
        let surface = try NativeEngineeringValkeySurface(entry: entry(for: .engineeringValkey), client: nil)

        try surface.cacheSection(viewModel).inspect().find(button: "Clear").tap()

        XCTAssertEqual(viewModel.pendingAction, .clearGroup("users"))
    }
}

private final class PendingFlushConcernBox {
    var value: EngineeringValkeyFlushConcern?

    init(_ value: EngineeringValkeyFlushConcern?) {
        self.value = value
    }
}
