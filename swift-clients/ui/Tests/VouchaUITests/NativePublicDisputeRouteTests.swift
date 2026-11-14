import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativePublicDisputeRouteTests: NativeRouteSurfaceViewModelTestCase {
    func testMyDisputesRouteLoadsCanonicalMemberProjection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/disputes"] = (
            ApiFixtureLoader.data("native.moderation.disputes.member.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/disputes")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases),
            client: makeClient(),
            routeMatch: route
        )

        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected the public dispute route to finish loading")
        }
        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.title, "post-1")
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first)
        XCTAssertEqual(url.path, "/api/v1/disputes")
        let query = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertTrue(query.contains(URLQueryItem(name: "mine", value: "true")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "limit", value: "25")))
    }

    func testMemberPaginationForwardsOpaqueCursorAndDeduplicatesCanonicalIdentity() async throws {
        let canonical = ApiFixtureLoader.data("native.moderation.disputes.member.default")
        var firstPage = try XCTUnwrap(JSONSerialization.jsonObject(with: canonical) as? [String: Any])
        var pageInfo = try XCTUnwrap(firstPage["page_info"] as? [String: Any])
        let cursor = try XCTUnwrap(pageInfo["start_cursor"] as? String)
        pageInfo["end_cursor"] = cursor
        pageInfo["has_next_page"] = true
        firstPage["page_info"] = pageInfo
        // The second canonical page repeats the real identity; no synthetic dispute ID is needed.
        CannedFeedURLProtocol.queuedHandlers["/api/v1/disputes"] = try [
            (JSONSerialization.data(withJSONObject: firstPage), 200, 0),
            (canonical, 200, 0)
        ]
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/disputes")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases), client: makeClient(), routeMatch: route
        )

        await viewModel.load()
        await viewModel.loadMoreForwardRows()
        await viewModel.loadMoreForwardRows()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected the public dispute route to finish loading")
        }
        XCTAssertEqual(viewModel.rows.count, 1)
        XCTAssertEqual(viewModel.rows.first?.title, "post-1")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
        let lastURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let query = try XCTUnwrap(URLComponents(url: lastURL, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertTrue(query.contains(URLQueryItem(name: "after", value: cursor)))
        XCTAssertTrue(query.contains(URLQueryItem(name: "mine", value: "true")))
    }
}
