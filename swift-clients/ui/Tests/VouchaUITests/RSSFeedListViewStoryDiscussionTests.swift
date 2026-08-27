import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
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

        let rows = try sut.inspect().find(ViewType.List.self).forEach(0)
        XCTAssertNoThrow(try rows.vStack(0).hStack(1).view(FollowerDistributionActions.self, 1))
    }
}
