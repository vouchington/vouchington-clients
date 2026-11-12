import Foundation
import ViewInspector
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HackerNewsFeedTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    private func client() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    func testPostPagesMergeStoredReferencesAndResetClearsThem() async throws {
        let path = "/api/v1/feeds/posts/any"
        let hnPage = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data("native.posts.hacker-news.filtered")) as? [String: Any])
        let references = try XCTUnwrap(hnPage["post_hacker_news_references"] as? [String: Any])
        let reference = try XCTUnwrap(references.values.first)
        var page = try XCTUnwrap(JSONSerialization
            .jsonObject(with: ApiFixtureLoader.data("swift.posts.feed.default")) as? [String: Any])
        let firstId = "p1"
        page["post_hacker_news_references"] = [firstId: reference]
        page["page_info"] = ["has_next_page": true, "end_cursor": "opaque"]
        let first = try JSONSerialization.data(withJSONObject: page)
        page["post_hacker_news_references"] = ["second": reference, firstId: ["story_count": 9, "comment_count": 8]]
        page["page_info"] = ["has_next_page": false, "end_cursor": NSNull()]
        CannedFeedURLProtocol.queuedHandlers[path] = try [
            (first, 200, 0),
            (JSONSerialization.data(withJSONObject: page), 200, 0)
        ]
        let viewModel = PostsListViewModel(client: client())
        await viewModel.load()
        await viewModel.loadNextPage()
        XCTAssertEqual(viewModel.hackerNewsReferencesByPostId[firstId]?.storyCount, 9)
        XCTAssertNotNil(viewModel.hackerNewsReferencesByPostId["second"])
        let view = PostsListView(viewModel: viewModel, isSignedIn: false)
        XCTAssertFalse(try view.inspect().findAll(HackerNewsReferencesView.self).isEmpty)
        XCTAssertFalse(viewModel.supportsHackerNewsFilter)
        viewModel.reset()
        XCTAssertTrue(viewModel.hackerNewsReferencesByPostId.isEmpty)
    }

    func testRssAndPodcastPagesRetainStoredReferences() async throws {
        for (contentType, source, path) in [
            (ContentType.news, FeedSource.all, "/api/v1/rss-feed-items"),
            (.podcast, .all, "/api/v1/rss-feed-items"),
            (.news, .your, "/api/v1/feeds/rss_feed_items/any")
        ] {
            CannedFeedURLProtocol.reset()
            CannedFeedURLProtocol.handlers[path] = (
                ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list"),
                200
            )
            CannedFeedURLProtocol.handlers["/api/v1/hacker-news-config"] = (
                ApiFixtureLoader.data("native.hacker-news-config.default"),
                200
            )
            let viewModel = RSSFeedListViewModel(client: client(), contentType: contentType, feedSource: source)
            await viewModel.refreshHackerNewsReferencesVisibility()
            var page = try XCTUnwrap(JSONSerialization
                .jsonObject(with: ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list")) as? [String: Any])
            page["page_info"] = ["has_next_page": true, "end_cursor": "opaque"]
            let first = try JSONSerialization.data(withJSONObject: page)
            page["page_info"] = ["has_next_page": false, "end_cursor": NSNull()]
            page["rss_feed_item_hacker_news_references"] = ["item-1": ["story_count": 9, "comment_count": 8]]
            CannedFeedURLProtocol.queuedHandlers[path] = try [
                (first, 200, 0),
                (JSONSerialization.data(withJSONObject: page), 200, 0)
            ]
            await viewModel.load()
            await viewModel.loadNextPage()
            XCTAssertEqual(viewModel.hackerNewsReferencesByItemId["item-1"]?.storyCount, 9)
            let view = RSSFeedListView(
                viewModel: viewModel,
                playbackController: PodcastPlaybackController(client: client(), sessionManager: SessionManager(
                    client: client()
                )),
                isSignedIn: false
            )
            XCTAssertFalse(try view.inspect().findAll(HackerNewsReferencesView.self).isEmpty)
            viewModel.hasHackerNewsReferences = true
            await viewModel.hackerNewsFilterTask?.value
            XCTAssertEqual(
                CannedFeedURLProtocol.capturedURLs.last
                    .flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?.queryItems?
                    .first { $0.name == "has_hacker_news_references" }?.value,
                "true"
            )
            await viewModel.reload()
            XCTAssertFalse(viewModel.hackerNewsReferencesByItemId.isEmpty)
        }
    }

    func testGlobalPostFilterUsesSupportedBrowseRouteAndNormalizesBrowseIds() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/all"] = (
            ApiFixtureLoader.data("swift.posts.feed.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/hacker-news-config"] = (
            ApiFixtureLoader.data("native.hacker-news-config.default"),
            200
        )
        let model = PostsListViewModel(client: client(), feedType: "all", timeRange: .last30Days)
        XCTAssertTrue(model.supportsHackerNewsFilter)
        XCTAssertFalse(model.isHackerNewsFilterVisible)
        await model.refreshHackerNewsReferencesVisibility()
        XCTAssertTrue(model.isHackerNewsFilterVisible)
        model.filter = .reviews
        await model.filterReloadTask?.value
        let view = PostsListView(viewModel: model, isSignedIn: false)
        let toggle = try view.inspect().group().toolbar().itemGroup().toggle()
        XCTAssertEqual(try toggle.labelView().text().string(), "Seen or mentioned on Hacker News")
        try toggle.tap()
        await model.filterReloadTask?.value
        XCTAssertEqual(model.items.map(\.id), ["profile-review-1"])
        XCTAssertEqual(model.hackerNewsReferencesByPostId["profile-review-1"]?.storyCount, 1)
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(request.path, "/api/v1/posts")
        XCTAssertEqual(
            URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == "time_range" }?
                .value,
            "1m"
        )
        XCTAssertEqual(
            URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == "post_types" }?
                .value,
            "review"
        )
        XCTAssertEqual(
            URLComponents(url: request, resolvingAgainstBaseURL: false)?.queryItems?
                .first { $0.name == "has_hacker_news_references" }?.value,
            "true"
        )
        for scope in ["any", "follow_users", "follow_topics"] {
            XCTAssertFalse(PostsListViewModel(client: client(), feedType: scope).supportsHackerNewsFilter)
        }
    }

}
