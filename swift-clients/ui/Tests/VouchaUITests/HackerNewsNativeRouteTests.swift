import Foundation
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class HackerNewsNativeRouteTests: NativeRouteSurfaceViewModelTestCase {
    func testForwardNewsAndPodcastRowsShowStoredReferencesAndFilter() async throws {
        for path in ["/news", "/podcast-episodes", "/feed/news"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let model = try NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: makeClient(),
                routeMatch: route.match
            )
            let endpoint = path.hasPrefix("/feed/") ? "/api/v1/feeds/rss_feed_items/any" : "/api/v1/rss-feed-items"
            CannedFeedURLProtocol.handlers[endpoint] = (
                ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list"),
                200
            )
            await model.load()
            XCTAssertTrue(model.supportsHackerNewsFilter)
            XCTAssertTrue(model.rows.contains { $0.detail.contains("Seen on Hacker News") })
            let surface = NativeListSurface(viewModel: model)
            XCTAssertNoThrow(try surface.inspect().find(ViewType.Toggle.self))
            model.hasHackerNewsReferences = true
            await model.hackerNewsFilterTask?.value
            _ = try await model.loadRssFeedItemPage(client: makeClient(), after: "opaque")
            let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
            let query = URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems
            XCTAssertEqual(query?.first { $0.name == "has_hacker_news_references" }?.value, "true")
            XCTAssertEqual(query?.first { $0.name == "after" }?.value, "opaque")
        }
    }

    func testForwardPostRowsShowReferencesAndFilterKeepsPostType() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/reviews"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"),
            200
        )
        await model.load()
        XCTAssertTrue(model.rows.contains { $0.detail.contains("Seen on Hacker News") })
        model.hasHackerNewsReferences = true
        await model.hackerNewsFilterTask?.value
        _ = try await model.loadPostPage(client: makeClient(), after: "opaque")
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let query = URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems
        XCTAssertEqual(query?.first { $0.name == "post_types" }?.value, "review")
        XCTAssertEqual(query?.first { $0.name == "has_hacker_news_references" }?.value, "true")
    }

    func testSummaryVariantsAndUnsupportedFilter() throws {
        let model = try NativeRouteSurfaceViewModel(entry: entry(for: .feedNews), client: nil)
        for (stories, comments, expected) in [
            (0, 0, "Base"),
            (0, 2, "Base · Mentioned on Hacker News · 2"),
            (1, 0, "Base · Seen on Hacker News · 1")
        ] {
            let references = try JSONDecoder().decode(
                HackerNewsReferences.self,
                from: Data("{\"storyCount\":\(stories),\"commentCount\":\(comments)}"
                    .utf8)
            )
            XCTAssertEqual(
                UiMessages.string(model.hackerNewsDetail(.verbatim("Base"), references: references), locale: .english),
                expected
            )
        }
        XCTAssertEqual(
            UiMessages.string(model.hackerNewsDetail(.verbatim("Base"), references: nil), locale: .english),
            "Base"
        )
        let other = try NativeRouteSurfaceViewModel(entry: entry(for: .userProfile), client: nil)
        XCTAssertFalse(other.supportsHackerNewsFilter)
    }

    func testProjectedRssSourceCanOmitUrlAndTitleFallsBackToItemHost() async throws {
        var body = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list")) as? [String: Any])
        var items = try XCTUnwrap(body["rss_feed_items"] as? [String: [String: Any]])
        var item = try XCTUnwrap(items["item-1"])
        item["title"] = NSNull()
        item["data"] = ["title": NSNull(), "link": NSNull()]
        item["link"] = "https://example.test/source"
        item["rss_feed"] = ["title": NSNull(), "feed_type": "article"]
        items["item-1"] = item
        body["rss_feed_items"] = items
        let data = try JSONSerialization.data(withJSONObject: body)
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items"] = (data, 200)
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        let page = try await model.loadRssFeedItemPage(client: makeClient(), after: nil)
        XCTAssertEqual(page.rows.first?.row.title, "example.test")
        let rows = try await model.loadRows(for: .feedNews, client: makeClient())
        XCTAssertEqual(rows.first?.title, "example.test")
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let response = try decoder.decode(NativeRssFeedItemsResponse.self, from: data)
        let summary = try XCTUnwrap(response.rssFeedItems["item-1"])
        XCTAssertEqual(UiMessages.string(model.bookmarkedFeedItemTitle(summary), locale: .english), "example.test")
        let generic = try decoder.decode(NativeGenericListResponse.self, from: data)
        let result = try XCTUnwrap(generic.results.first)
        XCTAssertEqual(generic.hydratedEntity(for: result).url, "https://example.test/source")
        let feed = try NativeRouteSurfaceViewModel(entry: entry(for: .feedPosts), client: nil)
        XCTAssertEqual(feed.hackerNewsPostFeedEndpoint(after: "cursor").path, "/api/v1/feeds/posts/any")
    }

}
