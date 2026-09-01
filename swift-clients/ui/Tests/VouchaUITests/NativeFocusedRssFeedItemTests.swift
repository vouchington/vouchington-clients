import Foundation
import ViewInspector
@testable import VouchaAPI
import VouchaAuth
@testable import VouchaDesignSystem
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeFocusedRssFeedItemTests: NativeRouteSurfaceViewModelTestCase {
    private let fixtureItemId = "00000000-0000-7000-8000-000000007886"

    func testCanonicalFeedRoutesResolveFocusedItemQuery() throws {
        for path in ["/news", "/podcast-episodes", "/videos"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let viewModel = NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: nil,
                routeMatch: route.match,
                routeQuery: "rss_item=item%201"
            )

            XCTAssertEqual(viewModel.focusedRssFeedItemId, "item 1", path)
        }
    }

    func testNormalFeedRouteDoesNotEnterFocusedState() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news"))
        let viewModel = NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: nil,
            routeMatch: route.match,
            routeQuery: "sort=latest"
        )

        XCTAssertNil(viewModel.focusedRssFeedItemId)
    }

    func testRouteIdentityChangesWhenFocusedItemQueryChanges() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/news"))
        let list = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            routeQuery: nil
        )
        let focused = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            routeQuery: "rss_item=item-1"
        )

        let listIdentity = try list.inspect().group().id() as? String
        let focusedIdentity = try focused.inspect().group().id() as? String
        XCTAssertNotEqual(listIdentity, focusedIdentity)
    }

    func testFeedFamilyRoutesDoNotEnterFocusedState() throws {
        for path in ["/feed/news", "/feed/podcasts", "/feed/videos", "/feed/news/friends"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let viewModel = NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: nil,
                routeMatch: route.match,
                routeQuery: "rss_item=item-1"
            )

            XCTAssertNil(viewModel.focusedRssFeedItemId, path)
        }
    }

    func testFocusedItemLoadsFullModelBookmarksAndElection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/\(fixtureItemId)"] = (
            ApiFixtureLoader.data("native.rss-feed-item.detail.default"),
            200
        )
        let viewModel = try makeFocusedViewModel(itemId: fixtureItemId)

        await viewModel.load()

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/rss-feed-items/\(fixtureItemId)"]
        )
        XCTAssertEqual(viewModel.focusedRssFeedItem?.data?.title, "Test Article")
        XCTAssertEqual(viewModel.focusedRssFeedItem?.data?.contentSnippet, "A short snippet")
        XCTAssertEqual(viewModel.focusedRssFeedItemContentHtml, "<p>A short snippet</p>")
        XCTAssertEqual(
            viewModel.focusedRssFeedItem?.thumbnailURL,
            "https://images.example.test/rss-feed-item-detail.jpg"
        )
        XCTAssertEqual(viewModel.focusedRssFeedItemElection?.votesScoreNet, 5)
        XCTAssertEqual(viewModel.focusedRssFeedItemVote, .like)
        XCTAssertTrue(viewModel.focusedRssFeedItemBookmarks["save"] == true)
        guard case .loaded = viewModel.state else {
            return XCTFail("Expected focused detail to load")
        }
    }

    func testFocusedItemFailureCanRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (Data("{}".utf8), 500)
        let viewModel = try makeFocusedViewModel()

        await viewModel.load()
        guard case .error = viewModel.state else {
            return XCTFail("Expected a retryable error")
        }

        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (
            ApiFixtureLoader.data("native.rss-feed-item.detail.default"),
            200
        )
        await viewModel.load()

        guard case .loaded = viewModel.state else {
            return XCTFail("Expected retry to load the focused detail")
        }
        XCTAssertEqual(viewModel.focusedRssFeedItem?.id, fixtureItemId)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 2)
    }

    func testFocusedSurfaceRendersExpandedItemPlaybackAndExternalLinkWithoutSequenceControls() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (detailData(), 200)
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let controller = PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: controller)

        XCTAssertEqual(viewModel.focusedRssFeedItemVote, .like)
        XCTAssertEqual(try sut.inspect().find(text: "Focused episode").string(), "Focused episode")
        XCTAssertNoThrow(try sut.inspect().find(ViewType.View<NativeHtmlContent>.self))
        XCTAssertEqual(
            NativeHtmlContent.plainText(html: viewModel.focusedRssFeedItemContentHtml)
                .trimmingCharacters(in: .whitespacesAndNewlines),
            "Full episode article body"
        )
        XCTAssertThrowsError(try sut.inspect().find(text: "Full episode description"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Saved"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Hidden"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Previous"))
        XCTAssertThrowsError(try sut.inspect().find(text: "Next"))
    }

    func testFocusedSurfaceHidesPlaybackForArticle() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (articleDetailData(), 200)
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let controller = PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: controller)

        XCTAssertFalse(try XCTUnwrap(viewModel.focusedRssFeedItem).hasPlayableMedia)
        XCTAssertThrowsError(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
    }

    func testFocusedSurfaceShowsFollowerDistributionForSignedInViewer() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (articleDetailData(), 200)
        let viewModel = try makeFocusedViewModel()
        await viewModel.load()

        let sut = NativeFocusedRssFeedItemSurface(
            viewModel: viewModel,
            playbackController: nil,
            currentUserId: "viewer-1"
        )

        XCTAssertNoThrow(try sut.inspect().find(FollowerDistributionActions.self))
    }

    func testFocusedSurfaceFallsBackToDescriptionWhenHtmlIsMissing() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (fallbackDetailData(), 200)
        let viewModel = try makeFocusedViewModel()

        await viewModel.load()

        XCTAssertNil(viewModel.focusedRssFeedItemContentHtml)
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: nil)
        XCTAssertEqual(try sut.inspect().find(text: "Fallback article body").string(), "Fallback article body")
    }

    func testFocusedSurfaceHidesPlaybackForExternalOnlyMedia() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (externalOnlyAudioDetailData(), 200)
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let controller = PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: controller)

        XCTAssertFalse(try XCTUnwrap(viewModel.focusedRssFeedItem).hasPlayableMedia)
        XCTAssertThrowsError(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
    }

    func testFocusedSurfaceRendersEmbedOnlyVideoUnavailableWithOpenSource() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (embedOnlyVideoDetailData(), 200)
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let controller = PodcastPlaybackController(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
        )
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: controller)

        XCTAssertFalse(try XCTUnwrap(viewModel.focusedRssFeedItem).hasPlayableMedia)
        XCTAssertNoThrow(try sut.inspect().find(text: "Video unavailable"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 1)
    }

    func testFocusedSurfaceRendersEmbedOnlyVideoUnavailableWithoutPlaybackController() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (embedOnlyVideoDetailData(), 200)
        let viewModel = try makeFocusedViewModel()
        await viewModel.load()
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: nil)

        XCTAssertNoThrow(try sut.inspect().find(text: "Video unavailable"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 1)
    }

    func testFocusedSurfaceApprovedEmbedSuppressesUnavailableAccessory() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (Data(#"""
        { "rss_feed_item": { "id": "item-1", "rss_feed_id": "feed-1", "title": "Embed-only video", "link": "https://example.com/video", "media_type": "video", "video_id": "abc123", "video_platform": "youtube" },
          "rss_feed_item_embeds": { "item-1": { "source_url": "https://example.com/video", "player_url": "https://www.youtube-nocookie.com/embed/abc123" } } }
        """#.utf8), 200)
        let viewModel = try makeFocusedViewModel()
        await viewModel.load()
        let sut = NativeFocusedRssFeedItemSurface(viewModel: viewModel, playbackController: nil)

        XCTAssertNoThrow(try sut.inspect().find(button: "Play video"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Open source"))
        XCTAssertEqual(try sut.inspect().findAll(ViewType.Link.self).count, 1)
        XCTAssertThrowsError(try sut.inspect().find(text: "Video unavailable"))
    }

    func testFocusedSurfaceApprovedEmbedDoesNotSuppressAudioPlaybackAccessory() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (
            playableDetailWithApprovedEmbed(mediaType: "audio"),
            200
        )
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let sut = NativeFocusedRssFeedItemSurface(
            viewModel: viewModel,
            playbackController: PodcastPlaybackController(
                client: client,
                sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
            )
        )

        XCTAssertNoThrow(try sut.inspect().find(button: "Play"))
        XCTAssertNoThrow(try sut.inspect().find(ViewType.View<RSSFeedPlaybackAccessoryView>.self))
        XCTAssertNoThrow(try sut.inspect().find(button: "Play video"))
    }

    func testFocusedSurfaceApprovedEmbedDoesNotSuppressDirectVideoPlaybackAccessory() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feed-items/item-1"] = (
            playableDetailWithApprovedEmbed(mediaType: "video"),
            200
        )
        let client = try makeClient()
        let viewModel = try makeFocusedViewModel(client: client)
        await viewModel.load()
        let sut = NativeFocusedRssFeedItemSurface(
            viewModel: viewModel,
            playbackController: PodcastPlaybackController(
                client: client,
                sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage())
            )
        )

        XCTAssertNoThrow(try sut.inspect().find(ViewType.View<RSSFeedPlaybackAccessoryView>.self))
        XCTAssertNoThrow(try sut.inspect().find(button: "Play video"))
    }

    private func makeFocusedViewModel(
        client: APIClient? = nil,
        itemId: String = "item-1"
    ) throws -> NativeRouteSurfaceViewModel {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/podcast-episodes"))
        return try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: client ?? makeClient(),
            routeMatch: route.match,
            routeQuery: "rss_item=\(itemId)"
        )
    }

    private func detailData() -> Data {
        Data(#"""
        {
          "rss_feed_item": {
            "id": "item-1",
            "rss_feed_id": "feed-1",
            "title": "Focused episode",
            "description": "Full episode description",
            "link": "https://example.com/episode",
            "media_type": "audio",
            "enclosure_url": "https://example.com/episode.mp3",
            "enclosure_type": "audio/mpeg",
            "duration_seconds": 1800
          },
          "rss_feed_item_election": {
            "votes_score_net": 3,
            "votes_count_up": 4,
            "votes_count_down": 1,
            "my_vote": "like"
          },
          "election_vote": {
            "__entity_type": "election_vote",
            "user_id": "user-1",
            "entity_id": "item-1",
            "choice": "like",
            "created_at": "2026-07-14T00:00:00Z"
          },
          "bookmarks": { "item-1": { "save": true, "hide": true } },
          "rss_feed_item_thumbnail_url": { "item-1": "/api/v1/images/thumb" },
          "content_html": "<p>Full <strong>episode</strong> article body</p>"
        }
        """#.utf8)
    }

    private func articleDetailData() -> Data {
        Data(#"""
        {
          "rss_feed_item": {
            "id": "item-1",
            "rss_feed_id": "feed-1",
            "title": "Focused article",
            "link": "https://example.com/article",
            "media_type": "article"
          }
        }
        """#.utf8)
    }

    private func fallbackDetailData() -> Data {
        Data(#"""
        {
          "rss_feed_item": {
            "id": "item-1",
            "rss_feed_id": "feed-1",
            "title": "Fallback article",
            "description": "Fallback article body",
            "link": "https://example.com/article",
            "media_type": "article"
          },
          "content_html": null
        }
        """#.utf8)
    }

    private func externalOnlyAudioDetailData() -> Data {
        Data(#"""
        {
          "rss_feed_item": {
            "id": "item-1",
            "rss_feed_id": "feed-1",
            "title": "External episode",
            "link": "https://example.com/episode",
            "media_type": "audio"
          }
        }
        """#.utf8)
    }

    private func embedOnlyVideoDetailData() -> Data {
        Data(#"""
        {
          "rss_feed_item": {
            "id": "item-1",
            "rss_feed_id": "feed-1",
            "title": "Embed-only video",
            "link": "https://example.com/video",
            "media_type": "video",
            "video_id": "abc123",
            "video_platform": "youtube"
          }
        }
        """#.utf8)
    }

    private func playableDetailWithApprovedEmbed(mediaType: String) -> Data {
        let isAudio = mediaType == "audio"
        let extensionName = isAudio ? "mp3" : "mp4"
        let mimeType = isAudio ? "audio/mpeg" : "video/mp4"
        return Data("""
        { "rss_feed_item": {
            "id": "item-1", "rss_feed_id": "feed-1", "title": "Playable media",
            "link": "https://example.com/item-1",
            "media_content": {
              "url": "https://example.com/item-1.\(extensionName)", "type": "\(mimeType)",
              "medium": "\(mediaType)"
            }
          },
          "rss_feed_item_embeds": { "item-1": { "source_url": "https://example.com/item-1", "player_url": "https://www.youtube-nocookie.com/embed/video-1" } } }
        """.utf8)
    }
}
