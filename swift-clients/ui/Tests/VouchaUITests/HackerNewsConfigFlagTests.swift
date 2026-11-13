import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

/// The backend Hacker News reference flag gates the filter in every native list surface.
@MainActor
final class HackerNewsConfigFlagTests: NativeRouteSurfaceViewModelTestCase {
    private let configPath = "/api/v1/hacker-news-config"
    private let parameter = "has_hacker_news_references"

    private func serveConfig(visible: Bool) {
        CannedFeedURLProtocol.handlers[configPath] = visible
            ? (ApiFixtureLoader.data("native.hacker-news-config.default"), 200)
            : (Data(#"{"references_visible":false,"post_time_range_cap":"1y"}"#.utf8), 200)
    }

    private func serveContent() {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/all"] = (
            ApiFixtureLoader.data("swift.posts.feed.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items"] = (
            ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list"), 200
        )
    }

    private func sentParameter() -> Bool {
        CannedFeedURLProtocol.capturedURLs.contains {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)?.queryItems?
                .contains { $0.name == parameter } == true
        }
    }

    func testPostsFilterIsHiddenAndNeverSentWhenFlagIsOff() async throws {
        serveConfig(visible: false)
        serveContent()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all")
        XCTAssertFalse(model.isHackerNewsFilterVisible)

        await model.refreshHackerNewsReferencesVisibility()
        model.hasHackerNewsReferences = true
        await model.filterReloadTask?.value

        XCTAssertFalse(model.isHackerNewsFilterVisible)
        XCTAssertFalse(sentParameter())
        let view = PostsListView(viewModel: model, isSignedIn: false)
        XCTAssertThrowsError(try view.inspect().group().toolbar().itemGroup().toggle())
    }

    func testPostsFilterIsVisibleAndSentWhenFlagIsOn() async throws {
        serveConfig(visible: true)
        serveContent()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all")

        await model.refreshHackerNewsReferencesVisibility()
        model.hasHackerNewsReferences = true
        await model.filterReloadTask?.value

        XCTAssertTrue(model.isHackerNewsFilterVisible)
        XCTAssertTrue(sentParameter())
        let view = PostsListView(viewModel: model, isSignedIn: false)
        XCTAssertNoThrow(try view.inspect().group().toolbar().itemGroup().toggle())
    }

    func testPostsFilterStaysHiddenWhenConfigFailsAndSelectionDropsWhenFlagTurnsOff() async throws {
        CannedFeedURLProtocol.handlers[configPath] = (Data("{}".utf8), 503)
        serveContent()
        let model = try PostsListViewModel(client: makeClient(), feedType: "all")
        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertFalse(model.isHackerNewsFilterVisible)

        serveConfig(visible: true)
        await model.refreshHackerNewsReferencesVisibility()
        model.hasHackerNewsReferences = true
        await model.filterReloadTask?.value
        XCTAssertTrue(model.isHackerNewsFilterVisible)

        CannedFeedURLProtocol.handlers[configPath] = (Data("{}".utf8), 503)
        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(model.isHackerNewsFilterVisible, "A failed refresh keeps the last known value")

        serveConfig(visible: false)
        await model.refreshHackerNewsReferencesVisibility()
        await model.filterReloadTask?.value
        XCTAssertFalse(model.isHackerNewsFilterVisible)
        XCTAssertFalse(model.hasHackerNewsReferences)
    }

    func testRssFilterIsHiddenAndNeverSentWhenFlagIsOff() async throws {
        serveConfig(visible: false)
        serveContent()
        let model = try RSSFeedListViewModel(client: makeClient(), contentType: .news, feedSource: .all)

        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertFalse(model.isHackerNewsFilterVisible)
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value

        XCTAssertFalse(sentParameter())
    }

    func testRssFilterIsVisibleAndSentWhenFlagIsOn() async throws {
        serveConfig(visible: true)
        serveContent()
        let model = try RSSFeedListViewModel(client: makeClient(), contentType: .news, feedSource: .all)

        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(model.isHackerNewsFilterVisible)
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value

        XCTAssertTrue(sentParameter())
    }

    func testRouteFilterIsHiddenAndNeverSentWhenFlagIsOff() async throws {
        serveConfig(visible: false)
        serveContent()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)

        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(model.supportsHackerNewsFilter)
        XCTAssertFalse(model.isHackerNewsFilterVisible)
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value
        _ = try await model.loadPostPage(client: makeClient(), after: "opaque")

        XCTAssertFalse(sentParameter())
        XCTAssertThrowsError(try NativeListSurface(viewModel: model).inspect().find(ViewType.Toggle.self))
    }

    func testRouteFilterIsVisibleAndSentWhenFlagIsOn() async throws {
        serveConfig(visible: true)
        serveContent()
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)

        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(model.isHackerNewsFilterVisible)
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value
        _ = try await model.loadPostPage(client: makeClient(), after: "opaque")

        XCTAssertTrue(sentParameter())
        XCTAssertNoThrow(try NativeListSurface(viewModel: model).inspect().find(ViewType.Toggle.self))
    }

    func testRssFlagTurningOffReloadsFromTheFirstPageWithoutTheFilter() async throws {
        serveConfig(visible: true)
        serveContent()
        let model = try RSSFeedListViewModel(client: makeClient(), contentType: .news, feedSource: .all)
        await model.refreshHackerNewsReferencesVisibility()
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value
        let before = CannedFeedURLProtocol.capturedURLs.count

        serveConfig(visible: false)
        await model.refreshHackerNewsReferencesVisibility()
        await model.hackerNewsFilterTask?.value

        XCTAssertFalse(model.hasHackerNewsReferences)
        let reloads = CannedFeedURLProtocol.capturedURLs.dropFirst(before).filter {
            $0.path == "/api/v1/rss-feed-items"
        }
        let reload = try XCTUnwrap(reloads.last)
        let names = URLComponents(url: reload, resolvingAgainstBaseURL: false)?.queryItems?.map(\.name) ?? []
        XCTAssertFalse(names.contains(parameter))
        XCTAssertFalse(names.contains("after"))
    }

    func testRouteWithoutClientKeepsTheFilterHidden() async throws {
        let model = try NativeRouteSurfaceViewModel(entry: entry(for: .feedNews), client: nil)

        await model.refreshHackerNewsReferencesVisibility()

        XCTAssertFalse(model.isHackerNewsFilterVisible)
    }
}
