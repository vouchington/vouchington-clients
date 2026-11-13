import Foundation
@testable import VouchaFeatures
import XCTest

/// A route-selected filter must restart from the first page once the late config reveals the filter.
@MainActor
final class HackerNewsLateConfigTests: NativeRouteSurfaceViewModelTestCase {
    private let parameter = "has_hacker_news_references"

    private func postRequests(after count: Int = 0) -> [[String: String]] {
        CannedFeedURLProtocol.capturedURLs.dropFirst(count).filter { $0.path == "/api/v1/posts" }.map { url in
            let items = URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems ?? []
            return Dictionary(items.map { ($0.name, $0.value ?? "") }, uniquingKeysWith: { first, _ in first })
        }
    }

    func testRouteSelectedFilterRestartsFirstPageWhenConfigResolvesVisible() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hacker-news-config"] = (
            ApiFixtureLoader.data("native.hacker-news-config.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "\(parameter)=true"
        )
        XCTAssertTrue(model.hasHackerNewsReferences)

        await model.load()
        let initial = postRequests()
        XCTAssertEqual(initial.count, 1)
        XCTAssertNil(initial[0][parameter], "The filter is hidden until config resolves")

        let before = CannedFeedURLProtocol.capturedURLs.count
        await model.refreshHackerNewsReferencesVisibility()
        await model.hackerNewsFilterTask?.value

        let restarted = postRequests(after: before)
        XCTAssertEqual(restarted.count, 1)
        XCTAssertEqual(restarted[0][parameter], "true")
        XCTAssertNil(restarted[0]["after"], "The unfiltered cursor must be dropped")
        XCTAssertTrue(model.isHackerNewsFilterVisible)
        XCTAssertTrue(model.hasHackerNewsReferences)
        guard case .loaded = model.state else { return XCTFail("The filtered first page should be loaded") }
    }

    func testResolvingVisibleWithoutSelectionDoesNotReload() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/hacker-news-config"] = (
            ApiFixtureLoader.data("native.hacker-news-config.default"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)

        await model.refreshHackerNewsReferencesVisibility()

        XCTAssertNil(model.hackerNewsFilterTask)
        XCTAssertTrue(postRequests().isEmpty)
    }
}
