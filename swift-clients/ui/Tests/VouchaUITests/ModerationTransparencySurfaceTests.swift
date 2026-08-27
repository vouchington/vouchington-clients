import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class ModerationTransparencySurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testPaidMemberRendersAggregateRows() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("""
        {"range":"30d","buckets":[
          {"date":"2026-08-01","metric":"reports","category":"spam","count":20},
          {"date":"2026-08-01","metric":"appeals","category":"accept","count":15},
          {"date":"2026-08-01","metric":"moderation_actions","category":"remove","count":25}
        ]}
        """.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected loaded state")
        }
        XCTAssertEqual(viewModel.rows.count, 3)
        XCTAssertEqual(
            viewModel.rows.map(\.title),
            ["Reports · Spam", "Appeals · Accept", "Moderation actions · Remove"]
        )
        XCTAssertEqual(viewModel.rows.map(\.detail), [
            "Released Aug 1, 2026 · Spam · 20",
            "Released Aug 1, 2026 · Accept · 15",
            "Released Aug 1, 2026 · Remove · 25"
        ])
    }

    func testUnknownMetricsRenderAsAggregatedTransparencyActions() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("""
        {"range":"30d","buckets":[
          {"date":"2026-08-01","metric":"future_metric","category":"one","count":2},
          {"date":"2026-08-02","metric":"another_future_metric","category":"two","count":3}
        ]}
        """.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected loaded state")
        }
        XCTAssertEqual(viewModel.rows.count, 2)
        XCTAssertEqual(viewModel.rows.map(\.title), [
            "Moderation transparency · Other",
            "Moderation transparency · Other"
        ])
        XCTAssertEqual(viewModel.rows.map(\.detail), [
            "Released Aug 1, 2026 · Other · 2",
            "Released Aug 2, 2026 · Other · 3"
        ])
    }

    func testRouteRangeIsValidatedAndSentToTheEndpoint() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (
            Data(#"{"range":"7d","buckets":[]}"#.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "range=7d"
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.moderationTransparencyRange, .days7)
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(URLComponents(url: request, resolvingAgainstBaseURL: false)?.query, "range=7d")
    }

    func testAllRangeLoadsAndMergesAnOlderMonthPage() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("""
        {"range":"all","buckets":[
          {"date":"2026-08-01","metric":"reports","category":"spam","count":20}
        ],"next_cursor":"older-page"}
        """.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "range=all"
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.moderationTransparencyNextCursor, "older-page")
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("""
        {"range":"all","buckets":[
          {"date":"2025-08-01","metric":"reports","category":"harassment","count":15}
        ]}
        """.utf8), 200)

        await viewModel.loadOlderModerationTransparency()

        XCTAssertEqual(viewModel.rows.count, 2)
        XCTAssertEqual(viewModel.rows.map(\.detail), [
            "Released August 2026 · Spam · 20",
            "Released August 2025 · Harassment · 15"
        ])
        XCTAssertNil(viewModel.moderationTransparencyNextCursor)
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(
            URLComponents(url: request, resolvingAgainstBaseURL: false)?.query,
            "range=all&after=older-page"
        )
    }

    func testMembershipDowngradeRejectsLateAuthorizedAllTimeContinuation() async throws {
        let path = "/api/v1/moderation-transparency"
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers[path] = (Data("""
        {"range":"all","buckets":[
          {"date":"2026-08-01","metric":"reports","category":"spam","count":20}
        ],"next_cursor":"older-page"}
        """.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "range=all"
        )
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("""
            {"range":"all","buckets":[
              {"date":"2025-08-01","metric":"reports","category":"harassment","count":15}
            ]}
            """.utf8), 200, 0),
            (Data("{}".utf8), 403, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let continuation = Task { await viewModel.loadOlderModerationTransparency() }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: path, method: "GET").wait()
        let reloadRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let entitlementReload = Task { await viewModel.reloadAfterMembershipEntitlementChange() }
        _ = try await reloadRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await entitlementReload.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        await continuation.value

        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.icon, "lock")
        XCTAssertFalse(viewModel.rows.map(\.detail).contains("Released August 2025 · Harassment · 15"))
        XCTAssertFalse(viewModel.moderationTransparencyIsLoadingOlder)
    }

    func testAllTimeContinuationDowngradeReplacesPaidRowsWithLockedState() async throws {
        let path = "/api/v1/moderation-transparency"
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("""
            {"range":"all","buckets":[
              {"date":"2026-08-01","metric":"reports","category":"spam","count":20}
            ],"next_cursor":"older-page"}
            """.utf8), 200, 0),
            (Data("{}".utf8), 403, 0)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "range=all"
        )

        await viewModel.load()
        await viewModel.loadOlderModerationTransparency()

        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.icon, "lock")
        XCTAssertTrue(viewModel.moderationTransparencyBuckets.isEmpty)
        XCTAssertNil(viewModel.moderationTransparencyNextCursor)
        XCTAssertNil(viewModel.moderationTransparencyLoadMoreError)
        XCTAssertFalse(viewModel.moderationTransparencyIsLoadingOlder)
    }

    func testInvalidRouteRangeFallsBackToThirtyDays() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            routeQuery: "range=invalid"
        )

        XCTAssertEqual(viewModel.moderationTransparencyRange, .days30)
    }

    func testPaidMemberWithoutReleasedBucketsShowsEmptyState() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (
            Data(#"{"range":"30d","buckets":[]}"#.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected loaded state")
        }
        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.icon, "shield")
    }

    func testForbiddenShowsLockedStateWithoutAggregateRows() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("{}".utf8), 403)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected loaded state")
        }
        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.icon, "lock")
    }

    func testMissingEndpointShowsUnavailableState() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("{}".utf8), 404)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected loaded state")
        }
        XCTAssertEqual(viewModel.rows.first?.icon, "lock")
    }

    func testMembershipDowngradeClearsPaidRowsBeforeReloadingLockedState() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/moderation-transparency"))
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("""
        {"range":"30d","buckets":[{"date":"2026-08-01","metric":"reports","category":"all","count":20}]}
        """.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        XCTAssertEqual(viewModel.rows.count, 1)
        CannedFeedURLProtocol.handlers["/api/v1/moderation-transparency"] = (Data("{}".utf8), 403)

        await viewModel.reloadAfterMembershipEntitlementChange()

        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.icon, "lock")
    }
}
