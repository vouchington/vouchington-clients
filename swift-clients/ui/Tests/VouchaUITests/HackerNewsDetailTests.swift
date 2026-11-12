import Foundation
import ViewInspector
import VouchaAPI
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class HackerNewsDetailTests: NativeRouteSurfaceViewModelTestCase {
    func testFocusedRssDetailRendersStoredStoryAndCommentLinks() async throws {
        let itemId = "00000000-0000-7000-8000-000000007886"
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/\(itemId)"] = (
            ApiFixtureLoader.data("native.rss-feed-items.hacker-news.detail"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news"))
        let model = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match,
            routeQuery: "rss_item=\(itemId)"
        )
        await model.load()
        XCTAssertNotNil(model.focusedHackerNewsReferences)
        let view = NativeFocusedRssFeedItemSurface(viewModel: model, playbackController: nil)
        let references = try view.inspect().find(HackerNewsReferencesView.self)
        XCTAssertFalse(try references.findAll(ViewType.Link.self).isEmpty)
        await settleProductSignals(model)
    }

    func testThreadRootReceivesStoredReferencesAndResetClearsThem() throws {
        let client = try makeClient()
        let model = NativeCommentThreadViewModel(client: client, rootPostId: "comment-root")
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let envelope = try decoder.decode(
            PostEnvelope.self,
            from: ApiFixtureLoader.data("native.posts.hacker-news.detail")
        )
        model.apply(detailResponse: envelope)
        XCTAssertFalse(model.hackerNewsReferencesByPostId.isEmpty)
        model.resetThreadMetadata()
        XCTAssertTrue(model.hackerNewsReferencesByPostId.isEmpty)
    }

    func testProfilePostCardsRetainReferencesAcrossPages() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice"] = (
            ApiFixtureLoader.data("native.users.profile.default"), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (
            ApiFixtureLoader.data("native.posts.hacker-news.filtered"), 200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/alice/posts"))
        let model = try NativeRouteSurfaceViewModel(entry: route.entry, client: makeClient(), routeMatch: route.match)
        await model.load()
        XCTAssertFalse(model.profileHackerNewsReferences.isEmpty)
        let surface = NativeUserProfileSurface(
            entry: route.entry,
            viewModel: model,
            isSignedIn: true,
            turnstileSiteKey: nil,
            showSignIn: {},
            onNavigate: { _ in }
        )
        XCTAssertFalse(try surface.inspect().findAll(HackerNewsReferencesView.self).isEmpty)
        _ = try await model.userPostPage(userId: "user-abc", filter: .all, after: "opaque", client: makeClient())
        XCTAssertFalse(model.profileHackerNewsReferences.isEmpty)
    }

}
