import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class ProductSurfaceSignalTests: NativeRouteSurfaceViewModelTestCase {
    func testSignedOutPublicLandingPageDoesNotRecordAVisit() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = (publicPageData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            routeMatch: match,
            routeQuery: "utm_source=newsletter&utm_medium=email&ref=ignored"
        )

        await viewModel.load()
        await settleProductSignals(viewModel)

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.hasSuffix("/visits") })
        XCTAssertEqual(viewModel.rows.first?.title, "Bonus")
    }

    func testSignedInPublicLandingPageRecordsOneVisitWithWebAttribution() async throws {
        let visitPath = "/api/v1/landing-pages/page-1/visits"
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = (publicPageData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            routeMatch: match,
            routeQuery: "utm_source=newsletter&utm_medium=email&ref=ignored",
            isSignedIn: true
        )

        await viewModel.load()
        await settleProductSignals(viewModel)
        await viewModel.load()
        await settleProductSignals(viewModel)

        let visits = CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == visitPath }
        XCTAssertEqual(visits.count, 1)
        XCTAssertEqual(visits.first?.method, "POST")
        let body = try JSONSerialization.jsonObject(with: Data((visits.first?.body ?? "").utf8)) as? [String: String]
        XCTAssertEqual(body, ["utm_source": "newsletter", "utm_medium": "email"])
        XCTAssertEqual(viewModel.rows.first?.title, "Bonus")
    }

    func testVisitUsesRefWhenTheUtmSourceIsBlank() async throws {
        let visitPath = "/api/v1/landing-pages/page-1/visits"
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = (publicPageData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            routeMatch: match,
            routeQuery: "utm_source=%20&ref=partner&utm_campaign=",
            isSignedIn: true
        )

        await viewModel.load()
        await settleProductSignals(viewModel)

        let body = try JSONSerialization.jsonObject(
            with: Data((CannedFeedURLProtocol.capturedRequests.first { $0.url.path == visitPath }?.body ?? "").utf8)
        ) as? [String: String]
        XCTAssertEqual(body, ["utm_source": "partner"])
    }

    func testFailedVisitDoesNotFailThePageOrSendASecondRequest() async throws {
        let visitPath = "/api/v1/landing-pages/page-1/visits"
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = (publicPageData, 200)
        CannedFeedURLProtocol.handlers[visitPath] = (Data("{}".utf8), 500)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            routeMatch: match,
            isSignedIn: true
        )

        await viewModel.load()
        await settleProductSignals(viewModel)
        await viewModel.load()
        await settleProductSignals(viewModel)

        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == visitPath }.count, 1)
        guard case .loaded = viewModel.state else {
            return XCTFail("Expected the public page to stay loaded when the visit signal fails")
        }
    }

    func testOwnerLandingPageDoesNotRecordAVisit() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages"] = (Data(#"""
        {"results":[{"id":"page-1","title":"Home","slug":"home","is_default":true}]}
        """#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/my/landing-pages/candidates"] = (Data(#"""
        {"candidates":{"profile_links":[],"reviews":[],"referral_links":[]}}
        """#.utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .landingPages), client: makeClient())

        await viewModel.load()

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.contains("/visits") })
        XCTAssertEqual(viewModel.rows.first?.title, "Home")
    }

    func testRssSourceViewIsRecordedOnceAndAFailedSignalDoesNotHideTheSource() async throws {
        let viewPath = "/api/v1/rss-feeds/source-1/views"
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (Data(#"""
        {"results":[{"id":"source-1","title":"Native Source","feed_type":"article","rss_feed_url":{"url":"https://example.com/rss"}}]}
        """#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/source-1"] = (
            Data(#"{"can_view_latest_crawl":false}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers[viewPath] = (Data("{}".utf8), 500)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match,
            isSignedIn: true
        )

        await viewModel.load()
        await settleProductSignals(viewModel)
        await viewModel.load()
        await settleProductSignals(viewModel)

        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == viewPath }.count, 1)
        XCTAssertEqual(viewModel.rows.first?.title, "Native Source")
    }

    func testFocusedItemRecordsOneViewBeforeAFailedDetailRequest() async throws {
        let viewPath = "/api/v1/rss-feed-items/item-1/views"
        CannedFeedURLProtocol.handlers[viewPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (Data("{}".utf8), 500)
        let viewModel = try makeFocusedViewModel(isSignedIn: true)

        await viewModel.load()
        await settleProductSignals(viewModel)
        await viewModel.load()
        await settleProductSignals(viewModel)

        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == viewPath }.count, 1)
        guard case .error = viewModel.state else {
            return XCTFail("Expected the detail failure to remain visible")
        }
    }

    func testSignedOutLandingItemClickDoesNotPost() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .landingPages), client: makeClient())

        viewModel.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")

        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
        XCTAssertNil(viewModel.productSignalTask)
    }

    func testSignedOutRssSourceAndFocusedItemDoNotRecordViews() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (Data(#"""
        {"results":[{"id":"source-1","title":"Native Source","feed_type":"article","rss_feed_url":{"url":"https://example.com/rss"}}]}
        """#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/source-1"] = (
            Data(#"{"can_view_latest_crawl":false}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source"))
        let source = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )
        let focused = try makeFocusedViewModel()

        await source.load()
        await settleProductSignals(source)
        await focused.load()
        await settleProductSignals(focused)

        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path.hasSuffix("/views") })
        XCTAssertEqual(source.rows.first?.title, "Native Source")
    }

    func testLandingItemClickWithoutAClientDoesNotPost() throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .landingPages), client: nil)

        viewModel.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")

        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
        XCTAssertNil(viewModel.productSignalTask)
    }

    func testLandingItemActivationPostsOneClickPerActivation() async throws {
        let clickPath = "/api/v1/landing-pages/page-1/clicks"
        CannedFeedURLProtocol.handlers[clickPath] = (Data("{}".utf8), 500)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            isSignedIn: true
        )

        viewModel.recordPublicLandingItemClick(
            landingPageId: "page-1",
            itemId: "item-1",
            groupMemberId: "member-1"
        )
        await settleProductSignals(viewModel)
        viewModel.recordPublicLandingItemClick(landingPageId: "page-1", itemId: "item-1")
        await settleProductSignals(viewModel)

        let clicks = CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == clickPath }
        XCTAssertEqual(clicks.count, 2)
        XCTAssertEqual(clicks.map(\.method), ["POST", "POST"])
        let firstBody = try JSONSerialization.jsonObject(with: Data((clicks[0].body ?? "").utf8)) as? [String: String]
        XCTAssertEqual(firstBody, [
            "landing_page_item_id": "item-1",
            "group_member_id": "member-1"
        ])
        let secondBody = try JSONSerialization.jsonObject(with: Data((clicks[1].body ?? "").utf8)) as? [String: String]
        XCTAssertEqual(secondBody, ["landing_page_item_id": "item-1"])
    }

    private var publicPageData: Data {
        Data(#"""
        {"landing_page":{"id":"page-1","title":"Bonus","subtitle":"Public","slug":"bonus","is_default":false}}
        """#.utf8)
    }

    private func makeFocusedViewModel(isSignedIn: Bool = false) throws -> NativeRouteSurfaceViewModel {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/podcast-episodes"))
        return try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "rss_item=item-1",
            isSignedIn: isSignedIn
        )
    }
}
