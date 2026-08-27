@testable import VouchaFeatures
import XCTest

@MainActor
final class CommunityModerationTransparencyRangeRequestTests: NativeRouteSurfaceViewModelTestCase {
    func testPaidTransparencyRequestsSelectedAllRange() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-transparency"] = (
            Data(
                #"{"range":"all","buckets":[{"date":"2026-08-01","metric":"reports","category":"spam","count":20}]}"#
                    .utf8
            ),
            200
        )
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders")

        viewModel.moderationTransparencyRange = .all
        let rows = try await viewModel.loadCommunityModerationTransparencyRows(client: client)

        XCTAssertEqual(try request(path: "/api/v1/communities/builders/moderation-transparency").query, "range=all")
        XCTAssertEqual(rows.first?.detail, "Released August 2026 · Spam · 20")
    }

    func testStaffRawAndSupplementaryRequestsUseSelectedRange() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-analytics"] = (
            ApiFixtureLoader.data("web.communities.moderation-analytics.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-transparency"] = (
            Data(#"{"range":"7d","buckets":[]}"#.utf8),
            200
        )
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(client: client, slug: "builders", isSiteModerator: true)

        viewModel.moderationTransparencyRange = .days7
        _ = try await viewModel.loadModerationAnalyticsRows(
            client: client,
            revision: viewModel.communityLoadRevision,
            tab: viewModel.selectedTab
        )

        XCTAssertEqual(try request(path: "/api/v1/communities/builders/moderation-analytics").query, "range=7d")
        XCTAssertEqual(try request(path: "/api/v1/communities/builders/moderation-transparency").query, "range=7d")
    }

    func testPaidAllTimeTransparencyLoadsOlderBuckets() async throws {
        let path = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (transparencyPage(date: "2026-08-01", category: "spam", cursor: "older-page"), 200, 0),
            (Data(#"{"message":"offline"}"#.utf8), 503, 0),
            (transparencyPage(date: "2026-07-01", category: "harassment", cursor: nil), 200, 0)
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

        XCTAssertNotNil(viewModel.moderationTransparencyLoadMoreError)
        XCTAssertEqual(viewModel.moderationTransparencyNextCursor, "older-page")

        await viewModel.loadOlderCommunityModerationTransparency()

        XCTAssertEqual(viewModel.summary.rows.map(\.detail), [
            "Released August 2026 · Spam · 20",
            "Released July 2026 · Harassment · 20"
        ])
        XCTAssertNil(viewModel.moderationTransparencyNextCursor)
        XCTAssertNil(viewModel.moderationTransparencyLoadMoreError)
        XCTAssertEqual(transparencyQueries, [
            "range=all",
            "range=all&after=older-page",
            "range=all&after=older-page"
        ])
    }

    func testStaffAllTimeSupplementaryTransparencyLoadsOlderBuckets() async throws {
        let transparencyPath = "/api/v1/communities/builders/moderation-transparency"
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/moderation-analytics"] = (
            ApiFixtureLoader.data("web.communities.moderation-analytics.default"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers[transparencyPath] = [
            (transparencyPage(date: "2026-08-01", category: "spam", cursor: "older-page"), 200, 0),
            (transparencyPage(date: "2026-07-01", category: "harassment", cursor: nil), 200, 0)
        ]
        let client = try makeClient()
        let viewModel = CommunityDetailViewModel(
            client: client,
            slug: "builders",
            initialTab: .moderationAnalytics,
            isSiteModerator: true
        )
        viewModel.moderationTransparencyRange = .all
        viewModel.summary.rows = try await viewModel.loadModerationAnalyticsRows(
            client: client,
            revision: viewModel.communityLoadRevision,
            tab: viewModel.selectedTab
        )

        await viewModel.loadOlderCommunityModerationTransparency()

        XCTAssertEqual(viewModel.summary.rows.suffix(2).map(\.detail), [
            "Released August 2026 · Spam · 20",
            "Released July 2026 · Harassment · 20"
        ])
        XCTAssertEqual(transparencyQueries, ["range=all", "range=all&after=older-page"])
    }

    private func request(path: String) throws -> URLComponents {
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first { $0.path == path })
        return try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false))
    }

    private var transparencyQueries: [String?] {
        CannedFeedURLProtocol.capturedURLs
            .filter { $0.path == "/api/v1/communities/builders/moderation-transparency" }
            .map(\.query)
    }

    private func transparencyPage(
        range: String = "all",
        date: String,
        category: String,
        cursor: String?
    ) -> Data {
        let nextCursor = cursor.map { value in ",\"next_cursor\":\"\(value)\"" } ?? ""
        let json = #"{"range":"\#(range)","buckets":[{"date":"\#(date)","metric":"reports","category":"\#(category)","count":20}]\#(nextCursor)}"#
        return Data(json.utf8)
    }
}
