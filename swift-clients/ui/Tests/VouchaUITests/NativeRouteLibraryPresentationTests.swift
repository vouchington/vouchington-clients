import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class NativeRouteLibraryPresentationTests: NativeRouteSurfaceViewModelTestCase {
    func testNativeRouteReferralLoadersRenderLinksClicksAndSignupStates() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/referral-links"] = (
            Data(
                """
                {
                  "results": [{
                    "id": "link-1",
                    "user_id": "user-1",
                    "referral_program_id": "program-1",
                    "label": "Card link",
                    "url": "https://example.com/card",
                    "created_at": "2026-01-01T00:00:00Z",
                    "updated_at": "2026-01-01T00:00:00Z"
                  }],
                  "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
                }
                """.utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/my/referral-clicks"] = (
            Data(
                """
                {
                  "results": [{"id": "click-1"}, {"id": "click-2"}],
                  "clicks": {
                    "click-1": {
                      "id": "click-1",
                      "landing_url": "https://example.com/click",
                      "signed_up_at": null,
                      "user_id": null,
                      "created_at": "2026-01-01T00:00:00Z"
                    },
                    "click-2": {
                      "id": "click-2",
                      "landing_url": "https://example.com/signup",
                      "signed_up_at": "2026-01-02T00:00:00Z",
                      "user_id": "user-2",
                      "created_at": "2026-01-01T00:00:00Z"
                    }
                  },
                  "users": {},
                  "page_info": {"has_next_page": false, "end_cursor": null, "start_cursor": null}
                }
                """.utf8
            ),
            200
        )
        let client = try makeClient()
        let linksViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .referrals),
            client: client,
            routeMatch: NativeRouteMatch(path: "/my/referral-links", template: "/my/referral-links")
        )
        let clicksViewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .referrals),
            client: client,
            routeMatch: NativeRouteMatch(path: "/my/referral-clicks", template: "/my/referral-clicks")
        )

        let linkRows = try await linksViewModel.loadReferralRows(client: client)
        let clickRows = try await clicksViewModel.loadReferralRows(client: client)

        XCTAssertEqual(linkRows.map(\.title), ["Card link"])
        XCTAssertEqual(linkRows.map(\.detail), ["user-1"])
        XCTAssertEqual(clickRows.map(\.title), [
            "https://example.com/click",
            "https://example.com/signup"
        ])
        XCTAssertEqual(clickRows.map(\.detail), ["Click", "Signup"])
    }

    func testNativeRoutePlanLoaderFormatsKnownAndUnknownIntervals() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/memberships/plans"] = (
            Data(
                """
                {
                  "plans": {
                    "plus": [
                      {
                        "id": "month",
                        "plan": "plus",
                        "price": { "amount": 1299, "currency": "usd" },
                        "interval": "monthly",
                        "stripe_price_id": "month"
                      },
                      {
                        "id": "year",
                        "plan": "plus",
                        "price": { "amount": 9999, "currency": "usd" },
                        "interval": "yearly",
                        "stripe_price_id": "year"
                      },
                      {
                        "id": "custom",
                        "plan": "plus",
                        "price": { "amount": 500, "currency": "eur" },
                        "interval": "lifetime",
                        "stripe_price_id": "custom"
                      }
                    ]
                  }
                }
                """.utf8
            ),
            200
        )
        let client = try makeClient()
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .plans), client: client)

        let rows = try await viewModel.loadPlanRows(client: client)

        XCTAssertEqual(rows.count, 3)
        XCTAssertTrue(rows.map(\.detail).contains("$12.99 per month"))
        XCTAssertTrue(rows.map(\.detail).contains("$99.99 per year"))
        XCTAssertTrue(rows.map(\.detail).contains("€5.00 per lifetime"))
    }

    func testTopicRecommendationCreateRouteBuildsDeterministicRowsWithoutNetwork() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/create"))
        let client = try makeClient()
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client,
            routeMatch: route.match
        )

        let rows = try await viewModel.loadTopicRecommendationRows(client: client)

        XCTAssertEqual(rows.map(\.title), ["Create recommendation", "Topic details"])
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

}
