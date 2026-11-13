import Foundation
@testable import VouchaFeatures
import XCTest

/// User-initiated refreshes retry the Hacker News flag so a failed initial fetch does not hide the
/// filter forever, and an unavailable config never blocks the list itself.
@MainActor
final class HackerNewsRefreshRetryTests: NativeRouteSurfaceViewModelTestCase {
    private let configPath = "/api/v1/hacker-news-config"

    private func failConfig() {
        CannedFeedURLProtocol.handlers[configPath] = (Data("{}".utf8), 503)
    }

    private func serveVisibleConfig() {
        CannedFeedURLProtocol.handlers[configPath] = (
            ApiFixtureLoader.data("native.hacker-news-config.default"), 200
        )
    }

    private func serveContent() {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items"] = (
            ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list"), 200
        )
    }

    func testPostsRefreshRetriesFailedConfigWhileTheListStillLoads() async throws {
        failConfig()
        serveContent()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all")
        await model.refreshHackerNewsReferencesVisibility()
        await model.reload()
        XCTAssertFalse(model.isHackerNewsFilterVisible)

        serveVisibleConfig()
        await model.refresh()

        XCTAssertTrue(model.isHackerNewsFilterVisible)
        guard case .loaded = model.state else { return XCTFail("The list should load") }
    }

    func testPostsRefreshLoadsTheListWhenConfigStaysUnavailable() async throws {
        failConfig()
        serveContent()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all")

        await model.refresh()

        XCTAssertFalse(model.isHackerNewsFilterVisible)
        guard case .loaded = model.state else { return XCTFail("An unavailable config must not block the list") }
    }

    func testRssRefreshRetriesFailedConfigWhileTheListStillLoads() async throws {
        failConfig()
        serveContent()
        let model = try RSSFeedListViewModel(client: makeClient(), contentType: .news, feedSource: .all)
        await model.refreshHackerNewsReferencesVisibility()
        await model.load()
        XCTAssertFalse(model.isHackerNewsFilterVisible)

        serveVisibleConfig()
        await model.refresh()

        XCTAssertTrue(model.isHackerNewsFilterVisible)
        guard case .loaded = model.state else { return XCTFail("The list should load") }
    }

    func testNativeRouteRetryRetriesFailedConfig() async throws {
        failConfig()
        serveContent()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        await model.refreshHackerNewsReferencesVisibility()
        await model.load()
        XCTAssertFalse(model.isHackerNewsFilterVisible)

        serveVisibleConfig()
        await nativeRetryAction(for: model)?()

        XCTAssertTrue(model.isHackerNewsFilterVisible)
    }

    /// A config response that arrives after a newer request started must not publish: the newer
    /// request owns the flag. A generation bump while the first request is in flight stands in for
    /// the newer request, which keeps the ordering deterministic.
    func testSupersededConfigResponsesDoNotPublish() async throws {
        serveVisibleConfig()
        serveContent()
        let posts = try PostsListViewModel(client: makeClient(), feedType: "all")
        let rss = try RSSFeedListViewModel(client: makeClient(), contentType: .news, feedSource: .all)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let surface = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)

        let stalePosts = Task { await posts.refreshHackerNewsReferencesVisibility() }
        let staleRss = Task { await rss.refreshHackerNewsReferencesVisibility() }
        let staleSurface = Task { await surface.refreshHackerNewsReferencesVisibility() }
        for _ in 0 ..< 500 where posts.hackerNewsConfigGeneration == 0
            || rss.hackerNewsConfigGeneration == 0 || surface.hackerNewsConfigGeneration == 0 {
            await Task.yield()
        }
        posts.hackerNewsConfigGeneration += 1
        rss.hackerNewsConfigGeneration += 1
        surface.hackerNewsConfigGeneration += 1
        await stalePosts.value
        await staleRss.value
        await staleSurface.value

        XCTAssertFalse(posts.hackerNewsReferencesVisible)
        XCTAssertFalse(rss.hackerNewsReferencesVisible)
        XCTAssertFalse(surface.hackerNewsReferencesVisible)

        await posts.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(posts.hackerNewsReferencesVisible)
    }
}
