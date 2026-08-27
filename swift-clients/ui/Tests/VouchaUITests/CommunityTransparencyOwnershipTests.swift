@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityTransparencyOwnershipTests: NativeRouteSurfaceViewModelTestCase {
    func testLateInitialRangeResponseCannotReplaceNewerRangeBucketsOrCursor() async throws {
        let path = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (transparencyPage(
                range: "all", date: "2026-08-01", category: "spam", cursor: "stale-cursor"
            ), 200, 0),
            (transparencyPage(
                range: "7d", date: "2026-08-14", category: "harassment", cursor: "current-cursor"
            ), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderationAnalytics
        )
        viewModel.moderationTransparencyRange = .all
        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleLoad = Task { try await viewModel.loadCommunityModerationTransparencyRows(client: client) }
        _ = try await staleRequest.wait()

        viewModel.moderationTransparencyRange = .days7
        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let currentLoad = Task { try await viewModel.loadCommunityModerationTransparencyRows(client: client) }
        _ = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        _ = try await currentLoad.value
        CannedFeedURLProtocol.releaseResponse(path: path)
        _ = try await staleLoad.value

        XCTAssertEqual(viewModel.moderationTransparencyBuckets.first?.category, "harassment")
        XCTAssertEqual(viewModel.moderationTransparencyNextCursor, "current-cursor")
        XCTAssertNil(viewModel.moderationTransparencyLoadMoreError)
    }

    func testAllTimeContinuationForbiddenOrNotFoundReplacesPaidRowsWithLockedState() async throws {
        let path = "/api/v1/communities/builders/moderation-transparency"
        for status in [403, 404] {
            CannedFeedURLProtocol.queuedHandlers[path] = [
                (transparencyPage(date: "2026-08-01", category: "spam", cursor: "older-page"), 200, 0),
                (Data("{}".utf8), status, 0)
            ]
            let client = try makeClient()
            let viewModel = CommunityDetailViewModel(
                client: client,
                slug: "builders",
                initialTab: .moderationAnalytics
            )
            viewModel.moderationTransparencyRange = .all
            viewModel.summary.rows = try await viewModel.loadCommunityModerationTransparencyRows(client: client)

            await viewModel.loadOlderCommunityModerationTransparency()

            XCTAssertEqual(viewModel.summary.rows.count, 1)
            XCTAssertEqual(viewModel.summary.rows.first?.icon, "lock")
            XCTAssertTrue(viewModel.moderationTransparencyBuckets.isEmpty)
            XCTAssertNil(viewModel.moderationTransparencyNextCursor)
            XCTAssertNil(viewModel.moderationTransparencyLoadMoreError)
            XCTAssertFalse(viewModel.moderationTransparencyIsLoadingOlder)
        }
    }

    func testLateStaffAnalyticsResponseCannotReplaceCurrentRangeRows() async throws {
        let analyticsPath = "/api/v1/communities/builders/moderation-analytics"
        let transparencyPath = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.queuedHandlers[analyticsPath] = [
            (moderationAnalytics(totalReports: 10), 200, 0),
            (moderationAnalytics(totalReports: 20), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[transparencyPath] = (
            transparencyPage(range: "7d", date: "2026-08-14", category: "harassment", cursor: nil),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: analyticsPath)
        defer { CannedFeedURLProtocol.releaseResponse(path: analyticsPath) }
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderationAnalytics,
            isSiteModerator: true
        )
        viewModel.moderationTransparencyRange = .all
        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: analyticsPath, method: "GET")
        let staleLoad = Task {
            try await viewModel.loadModerationAnalyticsRows(
                client: client,
                revision: viewModel.communityLoadRevision,
                tab: viewModel.selectedTab
            )
        }
        _ = try await staleRequest.wait()

        viewModel.moderationTransparencyRange = .days7
        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: analyticsPath, method: "GET")
        let currentLoad = Task {
            try await viewModel.loadModerationAnalyticsRows(
                client: client,
                revision: viewModel.communityLoadRevision,
                tab: viewModel.selectedTab
            )
        }
        _ = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: analyticsPath)
        let currentRows = try await currentLoad.value
        CannedFeedURLProtocol.releaseResponse(path: analyticsPath)
        _ = try await staleLoad.value

        XCTAssertEqual(currentRows.first?.detail, "20 reports · 0 pending")
        XCTAssertEqual(viewModel.moderationAnalyticsRows.first?.detail, "20 reports · 0 pending")
        XCTAssertEqual(viewModel.moderationTransparencyBuckets.first?.category, "harassment")
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == transparencyPath }.count,
            1
        )
    }

    func testStaleContinuationCannotClearCurrentLoadingState() async throws {
        let path = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.handlers[path] = (
            transparencyPage(date: "2026-08-01", category: "spam", cursor: "stale-page"),
            200
        )
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderationAnalytics
        )
        viewModel.moderationTransparencyRange = .all
        viewModel.summary.rows = try await viewModel.loadCommunityModerationTransparencyRows(client: client)
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (transparencyPage(date: "2025-08-01", category: "harassment", cursor: nil), 200, 0),
            (transparencyPage(date: "2026-08-01", category: "spam", cursor: "current-page"), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let staleRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleContinuation = Task { await viewModel.loadOlderCommunityModerationTransparency() }
        _ = try await staleRequest.wait()
        let currentRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let currentLoad = Task { try await viewModel.loadCommunityModerationTransparencyRows(client: client) }
        _ = try await currentRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        _ = try await currentLoad.value
        viewModel.moderationTransparencyIsLoadingOlder = true
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleContinuation.value

        XCTAssertTrue(viewModel.moderationTransparencyIsLoadingOlder)
        XCTAssertEqual(viewModel.moderationTransparencyNextCursor, "current-page")
        XCTAssertEqual(viewModel.moderationTransparencyBuckets.first?.category, "spam")
    }

    func testTransparencyDenialPreservesOnlyDurableSiteStaffAnalytics() async throws {
        for (isSiteModerator, status, expectedRawRowCount) in [
            (true, 403, 5),
            (false, 403, 0),
            (true, 404, 0)
        ] {
            try await assertTransparencyDenial(
                isSiteModerator: isSiteModerator,
                isAdministrator: false,
                status: status,
                expectedRawRowCount: expectedRawRowCount
            )
        }
    }

    func testAdministratorOnlyForbiddenTransparencyClearsRawAnalytics() async throws {
        try await assertTransparencyDenial(
            isSiteModerator: false,
            isAdministrator: true,
            status: 403,
            expectedRawRowCount: 0
        )
    }

    private func assertTransparencyDenial(
        isSiteModerator: Bool,
        isAdministrator: Bool,
        status: Int,
        expectedRawRowCount: Int
    ) async throws {
        let analyticsPath = "/api/v1/communities/builders/moderation-analytics"
        let transparencyPath = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.handlers[analyticsPath] = (moderationAnalytics(totalReports: 20), 200)
        CannedFeedURLProtocol.handlers[transparencyPath] = (Data("{}".utf8), status)
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderationAnalytics,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator
        )

        let rows = try await viewModel.loadModerationAnalyticsRows(
            client: client,
            revision: viewModel.communityLoadRevision,
            tab: viewModel.selectedTab
        )

        XCTAssertEqual(viewModel.moderationAnalyticsRows.count, expectedRawRowCount)
        XCTAssertEqual(rows.count, expectedRawRowCount + 1)
        XCTAssertEqual(rows.last?.icon, "lock")
        XCTAssertTrue(viewModel.moderationTransparencyBuckets.isEmpty)
        XCTAssertNil(viewModel.moderationTransparencyNextCursor)
    }

    private func moderationAnalytics(totalReports: Int) -> Data {
        let fixture = ApiFixtureLoader.data("web.communities.moderation-analytics.default")
        let json = String(decoding: fixture, as: UTF8.self)
            .replacingOccurrences(of: #""total_reports": 0"#, with: #""total_reports": \#(totalReports)"#)
        return Data(json.utf8)
    }

    private func transparencyPage(
        range: String = "all",
        date: String,
        category: String,
        cursor: String?
    ) -> Data {
        let nextCursor = cursor.map { value in ",\"next_cursor\":\"\(value)\"" } ?? ""
        let json =
            #"{"range":"\#(range)","buckets":[{"# +
            #""date":"\#(date)","metric":"reports","# +
            #""category":"\#(category)","count":20}]\#(nextCursor)}"#
        return Data(json.utf8)
    }
}
