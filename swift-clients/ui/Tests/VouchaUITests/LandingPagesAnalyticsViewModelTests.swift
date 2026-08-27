import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class LandingPagesAnalyticsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testFreeMemberShowsUpgradeStateWithoutRequestingAnalytics() async throws {
        let viewModel = try LandingPagesViewModel(client: makeClient(), canViewAnalytics: false)
        viewModel.setActivePage(makePage())

        await viewModel.loadSelectedPageAnalytics()

        XCTAssertTrue(viewModel.isAnalyticsPaidAccessRequired)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testExpiredMembershipDoesNotRequestLandingAnalytics() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/memberships/me"] = (
            Data(
                #"{"membership":{"id":"membership-1","user_id":"user-1","plan":"plus","status":"active","started_at":"2026-01-01T00:00:00Z","expires_at":"2020-01-01T00:00:00Z","granted_by_id":null,"cancelled_at":null,"expired_at":null,"past_due_at":null,"paused_at":null,"cancel_at_period_end":false,"latest_change_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","sku":{"id":"sku-1","plan":"plus","price":{"amount":500,"currency":"usd"},"interval":"monthly","stripe_price_id":"price-1","retired_at":null},"has_stripe_subscription":true}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try LandingPagesViewModel(
            client: makeClient(),
            canViewAnalytics: false,
            requiresAuthoritativeAnalyticsMembership: true
        )
        viewModel.setActivePage(makePage())

        await viewModel.loadSelectedPageAnalytics()

        XCTAssertTrue(viewModel.isAnalyticsPaidAccessRequired)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/memberships/me"])
    }

    func testLoadSelectedPageAnalyticsLoadsOwnerAnalyticsForSelectedPage() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-1/analytics"] = (
            Data("""
            {
              "analytics": {
                "total_visits": 18,
                "total_clicks": 6,
                "unique_visitors": 9,
                "ctr": 0.33,
                "item_clicks": [],
                "daily_stats": [],
                "utm_sources": [],
                "conversion_funnel": {
                  "total_visits": 18,
                  "total_clicks": 6,
                  "total_signups": 2,
                  "visit_to_click_rate": 0.33
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(makePage())

        await viewModel.loadSelectedPageAnalytics()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/landing-pages/page-1/analytics")
        XCTAssertEqual(viewModel.selectedPageAnalytics?.totalVisits, 18)
        if case .loaded = viewModel.selectedPageAnalyticsState {} else {
            XCTFail("Expected analytics state to be loaded")
        }
    }

    func testSetActivePageClearsAnalyticsWhenTheSamePageIsRebound() {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.setActivePage(makePage())
        viewModel.selectedPageAnalytics = LandingPageAnalytics(
            totalVisits: 18,
            totalClicks: 6,
            uniqueVisitors: 9,
            ctr: 0.33,
            itemClicks: [],
            dailyStats: [],
            utmSources: [],
            conversionFunnel: LandingPageConversionFunnel(
                totalVisits: 18,
                totalClicks: 6,
                totalSignups: 2,
                visitToClickRate: 0.33
            )
        )
        viewModel.selectedPageAnalyticsState = .loaded

        viewModel.setActivePage(
            LandingPageWithItems(
                id: "page-1",
                userId: "user-1",
                title: "Home Updated",
                subtitle: "Public profile",
                slug: "home",
                isDefault: true,
                createdAt: Date(timeIntervalSince1970: 1_717_000_000),
                updatedAt: Date(timeIntervalSince1970: 1_717_000_200),
                items: []
            )
        )

        XCTAssertEqual(viewModel.selectedPage?.title, "Home Updated")
        XCTAssertNil(viewModel.selectedPageAnalytics)
        if case .idle = viewModel.selectedPageAnalyticsState {} else {
            XCTFail("Expected analytics state to reset for the updated page")
        }
    }

    func testReloadRefreshesAnalyticsWhenTheSelectedPageDoesNotChange() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            ApiFixtureLoader.data("native.landing-pages.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            ApiFixtureLoader.data("native.landing-page-candidates.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1"] = (
            ApiFixtureLoader.data("native.landing-page-detail.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/landing-page-1/analytics"] = (
            analyticsData(totalVisits: 42), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        await viewModel.load()
        viewModel.selectedPageAnalytics = makeAnalytics(totalVisits: 18)
        viewModel.selectedPageAnalyticsState = .loaded

        await viewModel.reload()

        XCTAssertEqual(viewModel.selectedPage?.id, "landing-page-1")
        XCTAssertEqual(viewModel.selectedPageAnalytics?.totalVisits, 42)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter {
                $0.path == "/api/v1/my/landing-pages/landing-page-1/analytics"
            }.count,
            1
        )
    }

    func testNewestSamePageAnalyticsRequestWinsWhenResponsesFinishOutOfOrder() async throws {
        let path = "/api/v1/my/landing-pages/page-1/analytics"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (analyticsData(totalVisits: 18), 200, 0),
            (analyticsData(totalVisits: 42), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(makePage())
        let firstBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let olderRequest = Task { await viewModel.loadSelectedPageAnalytics() }
        _ = try await firstBarrier.wait()
        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let newerRequest = Task { await viewModel.loadSelectedPageAnalytics() }
        _ = try await secondBarrier.wait()

        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await newerRequest.value
        XCTAssertEqual(viewModel.selectedPageAnalytics?.totalVisits, 42)

        CannedFeedURLProtocol.releaseResponse(path: path)
        await olderRequest.value
        XCTAssertEqual(viewModel.selectedPageAnalytics?.totalVisits, 42)
    }

    func testSelectingTheCurrentPageReloadsAnalyticsAfterRebindingDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-1"] = (pageDetailData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-1/analytics"] = (
            analyticsData(totalVisits: 42), 200
        )
        let viewModel = try LandingPagesViewModel(client: makeClient())
        viewModel.setActivePage(makePage())
        viewModel.selectedPageAnalytics = makeAnalytics(totalVisits: 18)
        viewModel.selectedPageAnalyticsState = .loaded
        viewModel.title = "Draft title"
        viewModel.linkLabel = "Saved draft"
        viewModel.linkUrl = "https://example.com/saved-draft"
        XCTAssertTrue(viewModel.addLinkItem())
        let draftItems = viewModel.draftItems
        viewModel.linkLabel = "Unsubmitted label"
        viewModel.linkUrl = "https://example.com/unsubmitted"

        await viewModel.selectPage(id: "page-1")

        XCTAssertEqual(viewModel.selectedPage?.id, "page-1")
        XCTAssertEqual(viewModel.title, "Draft title")
        XCTAssertTrue(viewModel.hasUnsavedMetadata)
        XCTAssertEqual(viewModel.draftItems, draftItems)
        XCTAssertTrue(viewModel.hasUnsavedItems)
        XCTAssertEqual(viewModel.linkLabel, "Unsubmitted label")
        XCTAssertEqual(viewModel.linkUrl, "https://example.com/unsubmitted")
        XCTAssertEqual(viewModel.selectedPageAnalytics?.totalVisits, 42)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("/api/v1/my/landing-pages/page-1/analytics"), 1)
    }

    private func makePage() -> LandingPageWithItems {
        LandingPageWithItems(
            id: "page-1",
            userId: "user-1",
            title: "Home",
            subtitle: "Public profile",
            slug: "home",
            isDefault: true,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100),
            items: []
        )
    }

    private func makeAnalytics(totalVisits: Int) -> LandingPageAnalytics {
        LandingPageAnalytics(
            totalVisits: totalVisits,
            totalClicks: 6,
            uniqueVisitors: 9,
            ctr: 0.33,
            itemClicks: [],
            dailyStats: [],
            utmSources: [],
            conversionFunnel: LandingPageConversionFunnel(
                totalVisits: totalVisits,
                totalClicks: 6,
                totalSignups: 2,
                visitToClickRate: 0.33
            )
        )
    }

    private func analyticsData(totalVisits: Int) -> Data {
        Data(
            #"{"analytics":{"total_visits":\#(totalVisits),"total_clicks":6,"unique_visitors":9,"ctr":0.33,"item_clicks":[],"daily_stats":[],"utm_sources":[],"conversion_funnel":{"total_visits":\#(totalVisits),"total_clicks":6,"total_signups":2,"visit_to_click_rate":0.33}}}"#
                .utf8
        )
    }

    private var pageDetailData: Data {
        Data(
            #"{"landing_page":{"id":"page-1","user_id":"user-1","title":"Home Updated","subtitle":"Public profile","slug":"home","is_default":true,"created_at":"2024-05-27T00:26:40Z","updated_at":"2024-05-27T00:30:00Z","items":[]}}"#
                .utf8
        )
    }
}
