import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeLandingReviewRoutingTests: NativeRouteSurfaceViewModelTestCase {
    func testReviewActivationEscapesLiteralPercentAndPathSeparators() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry, client: makeClient(), routeMatch: route.match, isSignedIn: true
        )
        var object = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data("native.landing-page-detail.default")) as? [String: Any])
        object["user"] = ["username": "alice"]
        var pageObject = try XCTUnwrap(object["landing_page"] as? [String: Any])
        var items = try XCTUnwrap(pageObject["items"] as? [[String: Any]])
        var review = try XCTUnwrap(items[1]["review"] as? [String: Any])
        review["slug"] = "%2F/?#"
        items[1]["review"] = review
        pageObject["items"] = items
        object["landing_page"] = pageObject
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = try (
            JSONSerialization.data(withJSONObject: object), 200
        )
        await model.load()
        let page = try XCTUnwrap(model.publicLandingPage)
        var target: String?
        let sut = NativePublicLandingItemsSurface(page: page, viewModel: model, onNavigate: { target = $0 })
        try sut.inspect().find(viewWithAccessibilityIdentifier: "landing-item-item-review-1").button().tap()
        XCTAssertEqual(target, "/review/%252F%2F%3F%23")
        await settleProductSignals(model)
    }

}
