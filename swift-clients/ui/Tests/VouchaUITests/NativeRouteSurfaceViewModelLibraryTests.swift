import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelLibraryTests: NativeRouteSurfaceViewModelTestCase {
    func testListsLoadNativeListRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/lists"] = (
            Data("""
            {
              "results": [{ "__entity_type": "list", "id": "list-1" }],
              "page_info": { "has_next_page": false },
              "lists": {
                "list-1": {
                  "__entity_type": "list",
                  "id": "list-1",
                  "owner_user_id": "user-1",
                  "name": "Reading Queue",
                  "description": "Saved articles",
                  "visibility": "private",
                  "created_at": "2026-06-28T10:00:00Z",
                  "updated_at": "2026-06-28T10:00:00Z",
                  "removed_at": null
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .lists), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/lists")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "list.bullet.rectangle", title: "Reading Queue", detail: "Saved articles")
        )
    }

    func testBookmarksLoadSavedCollectionsAndIdentity() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        let savedResponse = Data(#"{"results":[{"id":"saved-1"}]}"#.utf8)
        for path in savedCollectionPaths {
            CannedFeedURLProtocol.handlers[path] = (savedResponse, 200)
        }

        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(
            Set(CannedFeedURLProtocol.capturedURLs.map(\.path)),
            Set(["/api/v1/my/identity"] + savedCollectionPaths)
        )
        XCTAssertEqual(viewModel.rows.map(\.title), [
            "Saved posts",
            "Saved feed items",
            "Saved URLs",
            "Saved communities"
        ])
    }

    func testLandingPagesLoadNativePagesAndCandidates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            Data("""
            {
              "results": [
                { "id": "page-1", "title": "Home", "subtitle": "Public profile", "slug": "home", "is_default": true },
                { "id": "page-2", "title": "Docs", "subtitle": "Reference", "slug": "docs", "is_default": false }
              ]
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (
            Data("""
            {
              "candidates": {
                "profile_links": [{ "id": "link-1" }],
                "reviews": [{ "id": "review-1" }],
                "referral_links": [{ "id": "ref-1" }]
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .landingPages), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/landing-pages",
            "/api/v1/my/landing-pages/candidates"
        ])
        XCTAssertEqual(viewModel.rows.map(\.title), ["Home", "Docs", "Landing page candidates"])
        XCTAssertEqual(viewModel.rows.first?.detail, "home · Default · Public profile")
    }

    func testMyLandingPageSlugLoadsSelectedPageDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (landingPagesData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-2"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-2",
                "title": "Docs",
                "slug": "docs",
                "items": [{ "id": "item-1" }, { "id": "item-2" }]
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-page/docs"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/landing-pages",
            "/api/v1/my/landing-pages/page-2"
        ])
        XCTAssertEqual(viewModel.rows.map(\.title), ["Docs", "Items"])
        XCTAssertEqual(viewModel.rows.last?.detail, "2 items · first item-1")
    }

    func testMyLandingPageSlugNamedAnalyticsLoadsSelectedPageDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (
            Data("""
            {
              "results": [
                { "id": "page-analytics", "title": "Analytics", "subtitle": null, "slug": "analytics", "is_default": false }
              ]
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-analytics"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-analytics",
                "title": "Analytics",
                "slug": "analytics",
                "items": [{ "id": "item-1" }, { "id": "item-2" }]
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-page/analytics"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/landing-pages",
            "/api/v1/my/landing-pages/page-analytics"
        ])
        XCTAssertEqual(viewModel.rows.map(\.title), ["Analytics", "Items"])
        XCTAssertEqual(viewModel.rows.last?.detail, "2 items · first item-1")
    }

    func testMyLandingPageAnalyticsRouteLoadsSelectedAnalytics() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (landingPagesData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/page-2/analytics"] = (
            Data(
                #"{"analytics":{"total_visits":12,"total_clicks":3,"unique_visitors":7,"ctr":0.25,"item_clicks":[],"daily_stats":[],"utm_sources":[],"conversion_funnel":{"total_visits":12,"total_clicks":3,"total_signups":2,"visit_to_click_rate":0.25}}}"#
                    .utf8
            ),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/landing-page/docs/analytics"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/landing-pages",
            "/api/v1/my/landing-pages/page-2/analytics"
        ])
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "chart.line.uptrend.xyaxis", title: "Docs", detail: "12 visits")
        )
        XCTAssertEqual(viewModel.rows.last?.detail, "2 signups")
    }

    func testMyReferralLinksLoadsNativeReferralLinksEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            Data("""
            {
              "results": [
                {
                  "id": "referral-link-1",
                  "user_id": "user-1",
                  "referral_program_id": "topic-1",
                  "url_id": "url-1",
                  "url": "https://example.com/referral",
                  "label": "Native referral",
                  "activated_at": "2026-01-01T00:00:00Z",
                  "deactivated_at": null,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z",
                  "referral_program_name": "Card",
                  "referral_program_slug": "card"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/referral-links")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .referrals),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/referral-links")
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "link", title: "Native referral", detail: "user-1"))
    }

    func testAdvancedSettingsLoadApiKeysPreferencesConsentsAndContributionStatus() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/api-keys"] = (Data(#"{"results":[{"id":"key-1"}]}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/aside-preferences"] = (
            Data(#"{"aside_preferences":[{"aside_key":"trending-communities"},{"aside_key":"trending-topics"}]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/consents"] = (Data(#"{"results":[{"id":"consent-1"}]}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/contribution-status"] = (
            Data("""
            {
              "contribution_status": { "allowed": true, "reason": null },
              "daily_quota": { "limit": 5, "used": 2 }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .advancedSettings), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path).sorted(), [
            "/api/v1/my/api-keys",
            "/api/v1/my/aside-preferences",
            "/api/v1/my/consents",
            "/api/v1/my/contribution-status"
        ])
        XCTAssertEqual(viewModel.rows.map(\.title), ["API Keys", "Dismissed asides", "Consents", "Contribution"])
        XCTAssertEqual(viewModel.rows.last?.detail, "Allowed · quota 2/5")
    }

    func testAccountSettingsLoadCurrentIdentity() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .accountSettings), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/my/identity")
        XCTAssertEqual(viewModel.rows.map(\.title), ["alice", "verified", "pro"])
    }

    func testProfileSettingsLoadProfile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/profile"] = (
            Data(#"{"profile":{"id":"user-1","markdown":"Native bio"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/profile/links"] = (
            Data(#"{"results":[{"id":"link-1"}]}"#.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .profileSettings), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(Set(CannedFeedURLProtocol.capturedURLs.map(\.path)), Set([
            "/api/v1/my/profile",
            "/api/v1/my/profile/links"
        ]))
        XCTAssertEqual(viewModel.rows.first?.title, "Bio")
        XCTAssertEqual(viewModel.rows.first?.detail, "Native bio")
        XCTAssertEqual(viewModel.rows.last?.title, "Profile links")
        XCTAssertEqual(viewModel.rows.last?.detail, "1 link")
    }

    private var savedCollectionPaths: [String] {
        [
            "/api/v1/users/user-1/posts/saved",
            "/api/v1/users/user-1/rss-feed-items/saved",
            "/api/v1/users/user-1/urls/saved",
            "/api/v1/users/user-1/communities/saved"
        ]
    }

    private var landingPagesData: Data {
        Data("""
        {
          "results": [
            { "id": "page-1", "title": "Home", "subtitle": "Public profile", "slug": "home", "is_default": true },
            { "id": "page-2", "title": "Docs", "subtitle": "Reference", "slug": "docs", "is_default": false }
          ]
        }
        """.utf8)
    }

    private var identityData: Data {
        PrivateUserTestFixture.identityEnvelope()
    }
}
