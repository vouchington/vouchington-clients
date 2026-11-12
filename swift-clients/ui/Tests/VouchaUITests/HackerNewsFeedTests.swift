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
        XCTAssertEqual(PostFilter.hackerNews.postTypes, nil)
        XCTAssertEqual(PostFilter.hackerNews.titleKey, .sharedHackerNewsFilterLabel)
        CannedFeedURLProtocol.handlers[path] = (first, 200)
        viewModel.filter = .hackerNews
        await viewModel.filterReloadTask?.value
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?
                .queryItems?.first { $0.name == "has_hacker_news_references" }?.value,
            "true"
        )
        viewModel.reset()
        XCTAssertTrue(viewModel.hackerNewsReferencesByPostId.isEmpty)
    }

    func testRssAndPodcastPagesRetainStoredReferences() async throws {
        for contentType in [ContentType.news, .podcast] {
            CannedFeedURLProtocol.reset()
            let path = "/api/v1/rss-feed-items"
            CannedFeedURLProtocol.handlers[path] = (
                ApiFixtureLoader.data("native.rss-feed-items.hacker-news.list"),
                200
            )
            let viewModel = RSSFeedListViewModel(client: client(), contentType: contentType, feedSource: .all)
            await viewModel.load()
            XCTAssertFalse(viewModel.hackerNewsReferencesByItemId.isEmpty)
            let view = RSSFeedListView(
                viewModel: viewModel,
                playbackController: PodcastPlaybackController(client: client(), sessionManager: SessionManager(
                    client: client(),
                    cookieStorage: HTTPCookieStorage()
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
}
