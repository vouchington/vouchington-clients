import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeAppLinkQueryTests: NativeRouteSurfaceViewModelTestCase {
    func testAppLinkQueryReachesSearchAndFocusedItemWithOneDecode() throws {
        for (encodedValue, decodedValue) in [("C%2B%2B", "C++"), ("a+b", "a b"), ("a%2Bb", "a+b")] {
            let searchURL = try XCTUnwrap(URL(string: "https://voucha.example/web-search?q=\(encodedValue)"))
            XCTAssertEqual(nativeAppLinkRouteQuery(searchURL), "q=\(encodedValue)")
            let searchRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
                for: nativeAppLinkRoutePathAndQuery(searchURL)
            ))
            let searchSurface = try NativeRouteDestinationSurface(
                entry: searchRoute.entry,
                client: nil,
                routeMatch: searchRoute.match,
                routeQuery: nativeAppLinkRouteQuery(searchURL),
                isSignedIn: false,
                showSignIn: {}
            )
            XCTAssertEqual(searchSurface.routeSearchQuery, decodedValue)

            let focusedURL = try XCTUnwrap(URL(string: "https://voucha.example/news?rss_item=\(encodedValue)"))
            let focusedRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(
                for: nativeAppLinkRoutePathAndQuery(focusedURL)
            ))
            let focusedViewModel = NativeRouteSurfaceViewModel(
                entry: focusedRoute.entry,
                client: nil,
                routeMatch: focusedRoute.match,
                routeQuery: nativeAppLinkRouteQuery(focusedURL)
            )
            XCTAssertEqual(focusedViewModel.focusedRssFeedItemId, decodedValue)
        }
    }
}
