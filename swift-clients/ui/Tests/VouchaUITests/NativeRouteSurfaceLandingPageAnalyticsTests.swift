import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceLandingPageAnalyticsTests: NativeRouteSurfaceViewModelTestCase {
    func testAdminUserProfileLoadsLandingPageAnalyticsRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data("""
            {
              "user": {
                "id": "target-user",
                "username": "alice",
                "name": "Alice",
                "description": "Public profile"
              },
              "profile_links": []
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                id: "admin-user",
                username: "admin",
                roles: ["administrator"]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/target-user/landing-pages"] = (
            Data("""
            {
              "results": [
                {
                  "id": "page-1",
                  "user_id": "target-user",
                  "title": "Alice Links",
                  "subtitle": null,
                  "slug": "links",
                  "is_default": true,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/landing-pages/page-1/analytics"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-1",
                "user_id": "target-user",
                "title": "Alice Links",
                "subtitle": null,
                "slug": "links",
                "is_default": true,
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "items": []
              },
              "analytics": {
                "total_visits": 20,
                "total_clicks": 5,
                "unique_visitors": 8,
                "ctr": 0.25,
                "item_clicks": [],
                "daily_stats": [],
                "utm_sources": [],
                "conversion_funnel": {
                  "total_visits": 20,
                  "total_clicks": 5,
                  "total_signups": 3,
                  "visit_to_click_rate": 0.25
                }
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/users/target-user/landing-pages"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/landing-pages/page-1/analytics"
        })
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.line.uptrend.xyaxis",
            title: "Analytics",
            detail: "1 page"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.bar.xaxis",
            title: "Alice Links",
            detail: "20 visits · 5 clicks · 3 signups"
        )))
    }

    func testAdminUserProfileKeepsLandingPageRowsWhenOneAnalyticsRequestFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data("""
            {
              "user": {
                "id": "target-user",
                "username": "alice",
                "name": "Alice",
                "description": "Public profile"
              },
              "profile_links": []
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                id: "admin-user",
                username: "admin",
                roles: ["administrator"]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/target-user/landing-pages"] = (
            Data("""
            {
              "results": [
                {
                  "id": "page-1",
                  "user_id": "target-user",
                  "title": "Alice Links",
                  "subtitle": null,
                  "slug": "links",
                  "is_default": true,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z"
                },
                {
                  "id": "page-2",
                  "user_id": "target-user",
                  "title": "Bonus Links",
                  "subtitle": "Seasonal",
                  "slug": "bonus",
                  "is_default": false,
                  "created_at": "2026-01-01T00:00:00Z",
                  "updated_at": "2026-01-01T00:00:00Z"
                }
              ],
              "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/landing-pages/page-1/analytics"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-1",
                "user_id": "target-user",
                "title": "Alice Links",
                "subtitle": null,
                "slug": "links",
                "is_default": true,
                "created_at": "2026-01-01T00:00:00Z",
                "updated_at": "2026-01-01T00:00:00Z",
                "items": []
              },
              "analytics": {
                "total_visits": 20,
                "total_clicks": 5,
                "unique_visitors": 8,
                "ctr": 0.25,
                "item_clicks": [],
                "daily_stats": [],
                "utm_sources": [],
                "conversion_funnel": {
                  "total_visits": 20,
                  "total_clicks": 5,
                  "total_signups": 3,
                  "visit_to_click_rate": 0.25
                }
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/landing-pages/page-2/analytics"] = (Data("{}".utf8), 404)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isAdministrator: true
        )

        await viewModel.load()

        if case .loaded = viewModel.state {} else {
            XCTFail("Expected state to be loaded")
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/users/target-user/landing-pages"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/landing-pages/page-1/analytics"
        })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/landing-pages/page-2/analytics"
        })
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.line.uptrend.xyaxis",
            title: "Analytics",
            detail: "2 pages"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.bar.xaxis",
            title: "Alice Links",
            detail: "20 visits · 5 clicks · 3 signups"
        )))
        XCTAssertTrue(viewModel.rows.contains(verbatimRow(
            icon: "chart.bar.xaxis",
            title: "Bonus Links",
            detail: "bonus · Seasonal"
        )))
    }

    func testAdminUserProfileKeepsProfileRowsWhenLandingPageListRequestFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            Data("""
            {
              "user": {
                "id": "target-user",
                "username": "alice",
                "name": "Alice",
                "description": "Public profile"
              },
              "profile_links": []
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(
                id: "admin-user",
                username: "admin",
                roles: ["administrator"]
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/users/target-user/landing-pages"] = (Data("{}".utf8), 503)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isAdministrator: true
        )

        await viewModel.load()

        if case .loaded = viewModel.state {} else {
            XCTFail("Expected state to be loaded")
        }
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "/api/v1/admin/users/target-user/landing-pages"
        })
        XCTAssertEqual(viewModel.userProfile.header?.user.name, "Alice")
        XCTAssertEqual(viewModel.userProfile.header?.user.username, "alice")
        XCTAssertFalse(viewModel.rows.contains {
            $0.title == "Landing page analytics"
        })
    }

    func testPublicLandingPageRouteLoadsAdminAnalyticsRowsForAdministrator() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-page"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-1",
                "title": "Bonus",
                "subtitle": "Public",
                "slug": "bonus",
                "is_default": false
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/landing-pages/page-1/analytics"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-1",
                "user_id": "user-1",
                "title": "Bonus",
                "subtitle": "Public",
                "slug": "bonus",
                "is_default": false,
                "created_at": "2026-07-01T00:00:00Z",
                "updated_at": "2026-07-01T00:00:00Z",
                "items": []
              },
              "analytics": {
                "total_visits": 20,
                "total_clicks": 5,
                "unique_visitors": 8,
                "ctr": 0.25,
                "item_clicks": [],
                "daily_stats": [],
                "utm_sources": [],
                "conversion_funnel": {
                  "total_visits": 20,
                  "total_clicks": 5,
                  "total_signups": 3,
                  "visit_to_click_rate": 0.25
                }
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/landing"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/users/alice/landing-page",
            "/api/v1/admin/landing-pages/page-1/analytics"
        ])
        XCTAssertEqual(viewModel.rows.map(\.title), [
            "Bonus",
            "Visits",
            "Clicks",
            "Unique visitors",
            "Signups"
        ])
        XCTAssertEqual(viewModel.rows[1].detail, "20 visits")
        XCTAssertEqual(viewModel.rows.last?.detail, "3 signups")
    }

    func testPublicLandingPageRouteKeepsPublicRowsWhenAdminAnalyticsFails() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-page"] = (
            Data("""
            {
              "landing_page": {
                "id": "page-1",
                "title": "Bonus",
                "subtitle": "Public",
                "slug": "bonus",
                "is_default": false
              }
            }
            """.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/admin/landing-pages/page-1/analytics"] = (Data("{}".utf8), 503)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/landing"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            isAdministrator: true
        )

        await viewModel.load()

        if case .loaded = viewModel.state {} else {
            XCTFail("Expected state to be loaded")
        }
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/users/alice/landing-page",
            "/api/v1/admin/landing-pages/page-1/analytics"
        ])
        XCTAssertEqual(viewModel.rows, [
            verbatimRow(icon: "doc.text", title: "Bonus", detail: "bonus · Public")
        ])
    }
}
