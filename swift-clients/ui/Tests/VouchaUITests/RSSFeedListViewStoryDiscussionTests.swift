import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RSSFeedListViewStoryDiscussionTests: XCTestCase {
    private let apiBaseURL = URL(string: "http://localhost:2999")!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
        CannedFeedURLProtocol.capturedBodies = []
    }

    private func makeClient() -> APIClient {
        APIClient(
            config: AppConfig(baseURL: apiBaseURL, turnstileSiteKey: "test-site-key"),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makeViewModel() -> RSSFeedListViewModel {
        RSSFeedListViewModel(client: makeClient(), contentType: .news)
    }

    private func makePlaybackController() -> PodcastPlaybackController {
        let client = makeClient()
        return PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
    }

    func testStoryDiscussionDestinationViewPreservesOfficialVoteRestriction() throws {
        let sut = RSSFeedListView(
            viewModel: makeViewModel(),
            playbackController: makePlaybackController(),
            canVote: false
        )
        let destination = StoryDiscussionDestination(postId: "post-1", postType: .discussion)

        let view = sut.storyDiscussionDestinationView(destination)
        let routedDestination = try view.inspect().find(NativeRouteDestinationView.self).actualView()

        XCTAssertTrue(routedDestination.routeIdentity.contains("|false|"))
    }

    func testOpenStoryDiscussionButtonTapsAndUsesExpectedLabel() throws {
        let sut = RSSFeedListView(
            viewModel: makeViewModel(),
            playbackController: makePlaybackController(),
            canVote: true
        )
        let destination = StoryDiscussionDestination(postId: "post-1", postType: .discussion)

        let button = sut.openStoryDiscussionButton(destination)
        let inspectedButton = try button.inspect().find(button: "Open discussion")

        try inspectedButton.tap()
    }

    func testRssListShowsFollowerDistributionForEachItemWhenSignedIn() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            ApiFixtureLoader.data("swift.rss-feed-items.feed.default"),
            200
        )
        let viewModel = makeViewModel()
        await viewModel.load()
        let sut = RSSFeedListView(
            viewModel: viewModel,
            playbackController: makePlaybackController(),
            currentUserId: "viewer-1"
        )

        XCTAssertNoThrow(try sut.inspect().find(FollowerDistributionActions.self))
    }

    func testRelatedArticlesRenderImmediatelyWithLoadedCountAndExplicitRetry() async throws {
        let primary = article("primary")
        let peer = article("peer")
        let model = makeViewModel()
        model.storyIdsByItemId[primary.id] = "story"
        let group = StoryRelatedArticles(
            primaryItemId: primary.id,
            items: [peer],
            pageInfo: .init(hasNextPage: true, endCursor: "opaque")
        )
        model.storyRelatedArticlesByStoryId["story"] = group
        let view = RSSFeedListView(viewModel: model, playbackController: makePlaybackController())
        let collapsed = view.storyRelatedArticlesView(for: primary)
        XCTAssertNoThrow(try collapsed.inspect().find(text: "1+ related articles"))
        XCTAssertTrue(try collapsed.inspect().findAll(ViewType.View<RssFeedItemCard>.self).isEmpty)
        try collapsed.inspect().find(ViewType.Button.self).tap()
        let expanded = view.storyRelatedArticlesView(for: primary)
        XCTAssertEqual(try expanded.inspect().findAll(ViewType.View<RssFeedItemCard>.self).count, 1)
        XCTAssertNoThrow(try expanded.inspect().find(button: "Load more"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        CannedFeedURLProtocol.handlers["/api/v1/stories/story"] = (Data("{}".utf8), 500)
        await model.loadMoreStoryArticles(rssFeedItemId: primary.id)
        let failed = view.storyRelatedArticlesView(for: primary)
        XCTAssertEqual(try failed.inspect().findAll(ViewType.View<RssFeedItemCard>.self).count, 1)
        XCTAssertNoThrow(try failed.inspect().find(button: "Try Again"))
        XCTAssertEqual(group.pagination.items.map(\.id), [peer.id])
        XCTAssertTrue(group.isExpanded)
    }

    func testRelatedArticlesExpanderCountsOnlyVisiblePeersAndKeepsContinuation() throws {
        let primary = article("primary")
        let hidden = article("hidden")
        let visible = article("visible")
        let model = makeViewModel()
        model.storyIdsByItemId[primary.id] = "story"
        model.hiddenItemIds.insert(hidden.id)
        let group = StoryRelatedArticles(
            primaryItemId: primary.id,
            items: [hidden, visible],
            pageInfo: .init(hasNextPage: false, endCursor: nil)
        )
        model.storyRelatedArticlesByStoryId["story"] = group
        let view = RSSFeedListView(viewModel: model, playbackController: makePlaybackController())

        XCTAssertTrue(model.canStartStoryDiscussion(rssFeedItemId: primary.id))
        XCTAssertNoThrow(try view.storyRelatedArticlesView(for: primary).inspect().find(text: "1 related article"))
        group.isExpanded = true
        XCTAssertEqual(
            try view.storyRelatedArticlesView(for: primary).inspect().findAll(ViewType.View<RssFeedItemCard>.self)
                .count,
            1
        )

        group.pagination.replaceItems([hidden])
        XCTAssertFalse(model.canStartStoryDiscussion(rssFeedItemId: primary.id))
        XCTAssertThrowsError(try view.storyRelatedArticlesView(for: primary).inspect().find(ViewType.Button.self))

        group.pagination.restoreContinuation(endCursor: "next", hasMore: true)
        XCTAssertTrue(model.canStartStoryDiscussion(rssFeedItemId: primary.id))
        XCTAssertNoThrow(try view.storyRelatedArticlesView(for: primary).inspect().find(text: "0+ related articles"))
        XCTAssertNoThrow(try view.storyRelatedArticlesView(for: primary).inspect().find(button: "Load more"))
    }

    private func article(_ id: String) -> RssFeedItem {
        RssFeedItem(
            id: id,
            rssFeedId: "feed",
            title: id,
            description: nil,
            content: nil,
            link: nil,
            publishedAt: nil,
            creator: nil,
            categories: nil,
            mediaContent: nil
        )
    }

}
